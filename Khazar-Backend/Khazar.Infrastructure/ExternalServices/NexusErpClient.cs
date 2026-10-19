using System.Net;
using System.Net.Http.Json;
using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Entities;
using Khazar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Khazar.Infrastructure.ExternalServices;

/// <summary>
/// Typed HttpClient for NexusERP. The HttpClient is created by IHttpClientFactory, so connection pooling
/// and DNS refresh are handled by the factory; BaseAddress and the timeout come from <see cref="NexusErpOptions"/>
/// and the JWT is attached by <see cref="NexusErpAuthHandler"/>.
/// A Khazar SKU is the barcode of the matching NexusERP product.
/// Every transport failure (connection refused, DNS failure, timeout, failed login, malformed response) is logged
/// and turned into the fallback return value; only cancellation requested by the caller is rethrown.
/// </summary>
public sealed class NexusErpClient : INexusErpClient
{
    private const string StocksEndpoint = "api/inventory/stocks";
    private const string ProductByBarcodeEndpoint = "api/products/barcode/";
    private const string SalesEndpoint = "api/sales";

    private readonly HttpClient _httpClient;
    private readonly KhazarDbContext _dbContext;
    private readonly NexusErpOptions _settings;
    private readonly ILogger<NexusErpClient> _logger;

    public NexusErpClient(
        HttpClient httpClient, KhazarDbContext dbContext, IOptions<NexusErpOptions> options, ILogger<NexusErpClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _dbContext = dbContext;
        _settings = options.Value;
        _logger = logger;
    }

    private string? WarehouseId => string.IsNullOrWhiteSpace(_settings.WarehouseId) ? null : _settings.WarehouseId.Trim();

    public async Task<int?> GetStockBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU is required.", nameof(sku));

        // SKUs are stored upper-case by ProductVariant.
        var normalizedSku = sku.Trim().ToUpperInvariant();

        try
        {
            // NexusERP has no per-product stock endpoint; this returns one row per product and warehouse.
            using var response = await _httpClient.GetAsync(StocksEndpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "NexusERP stock request for SKU {Sku} failed with HTTP {StatusCode}; falling back to local stock.",
                    normalizedSku, (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<ItemsResponse<StockRow>>(cancellationToken);
            if (payload?.Items is null)
            {
                _logger.LogWarning(
                    "NexusERP returned an invalid stock payload for SKU {Sku}; falling back to local stock.", normalizedSku);
                return null;
            }

            var rows = payload.Items
                .Where(r => string.Equals(r.Product?.Barcode, normalizedSku, StringComparison.OrdinalIgnoreCase)
                            && (WarehouseId is null || r.WarehouseId == WarehouseId))
                .ToList();

            // No row means either nothing was ever received into stock or NexusERP does not know the SKU.
            if (rows.Count == 0)
            {
                if (await FindProductIdAsync(normalizedSku, cancellationToken) is not null)
                    return 0;

                _logger.LogWarning("NexusERP does not know SKU {Sku}; live stock is unavailable.", normalizedSku);
                return null;
            }

            // NexusERP keeps fractional quantities (kg, m); the storefront sells whole units only.
            return (int)Math.Max(0, Math.Floor(rows.Sum(r => r.Quantity)));
        }
        catch (Exception ex) when (IsTransportFailure(ex, cancellationToken))
        {
            LogTransportFailure(ex, "stock lookup for SKU " + normalizedSku);
            return null;
        }
    }

    public async Task<bool> PushOrderToErpAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.Items.Count == 0)
        {
            _logger.LogWarning(
                "Order {OrderNumber} has no items and was not pushed to NexusERP; were the items loaded?", order.OrderNumber);
            return false;
        }

        if (string.IsNullOrWhiteSpace(_settings.StoreId))
        {
            _logger.LogError(
                "Order {OrderNumber} was not pushed: '{Section}:{Setting}' is not configured.",
                order.OrderNumber, NexusErpOptions.SectionName, nameof(NexusErpOptions.StoreId));
            return false;
        }

        // Order items snapshot names and prices, not SKUs; the variant still holds the SKU.
        var variantIds = order.Items.Select(i => i.VariantId).Distinct().ToList();
        var skuByVariantId = await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.SKU, cancellationToken);

        try
        {
            var productIdBySku = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = new List<SaleItemRequest>(order.Items.Count);

            foreach (var item in order.Items)
            {
                if (!skuByVariantId.TryGetValue(item.VariantId, out var sku))
                {
                    _logger.LogError(
                        "Order {OrderNumber} was not pushed: variant {VariantId} no longer exists, so its SKU is unknown.",
                        order.OrderNumber, item.VariantId);
                    return false;
                }

                if (!productIdBySku.TryGetValue(sku, out var productId))
                {
                    productId = await FindProductIdAsync(sku, cancellationToken);
                    if (productId is null)
                    {
                        _logger.LogWarning(
                            "Order {OrderNumber} was not pushed: NexusERP has no product with barcode {Sku}.",
                            order.OrderNumber, sku);
                        return false;
                    }

                    productIdBySku[sku] = productId;
                }

                // Storefront price, so the ERP document matches what the customer paid.
                lines.Add(new SaleItemRequest(productId, item.Quantity, item.UnitPrice));
            }

            var request = new CreateSaleRequest(_settings.StoreId.Trim(), WarehouseId, CustomerId: null, order.CreatedAt, lines);

            using var response = await _httpClient.PostAsJsonAsync(SalesEndpoint, request, cancellationToken);

            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
            {
                var sale = await response.Content.ReadFromJsonAsync<SaleCreatedResponse>(cancellationToken);
                _logger.LogInformation(
                    "Order {OrderNumber} was accepted by NexusERP as sale {DocumentNumber}.",
                    order.OrderNumber, sale?.DocumentNumber);
                return true;
            }

            _logger.LogWarning(
                "NexusERP rejected order {OrderNumber} with HTTP {StatusCode}: {Error}",
                order.OrderNumber, (int)response.StatusCode, await ReadErrorAsync(response, cancellationToken));
            return false;
        }
        catch (Exception ex) when (IsTransportFailure(ex, cancellationToken))
        {
            LogTransportFailure(ex, "order push for " + order.OrderNumber);
            return false;
        }
    }

    /// <summary>NexusERP id of the product with this barcode, or null when there is none. Other failures throw.</summary>
    private async Task<string?> FindProductIdAsync(string sku, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            ProductByBarcodeEndpoint + Uri.EscapeDataString(sku), cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ProductLookupResponse>(cancellationToken);
        return string.IsNullOrWhiteSpace(payload?.Product?.Id)
            ? throw new System.Text.Json.JsonException("NexusERP product lookup returned no product id.")
            : payload.Product.Id;
    }

    // NexusERP errors are { "ok": false, "error": "..." }; anything else (e.g. an empty 500) yields null.
    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return (await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken))?.Error;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            return null;
        }
    }

    // HttpClient reports its own timeout as TaskCanceledException; it is a failure only when the caller did not cancel.
    private static bool IsTransportFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException or System.Text.Json.JsonException or NotSupportedException
        || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    private void LogTransportFailure(Exception ex, string operation)
    {
        if (ex is OperationCanceledException)
            _logger.LogError(
                "NexusERP {Operation} timed out after {TimeoutSeconds} seconds.",
                operation, _httpClient.Timeout.TotalSeconds);
        else
            _logger.LogError(ex, "NexusERP {Operation} failed: NexusERP is unreachable or returned an invalid response.", operation);
    }

    // Contract of NexusERP's POST api/sales (operation Sales_Create) and the responses read here.
    private sealed record CreateSaleRequest(
        string StoreId,
        string? WarehouseId,
        string? CustomerId,
        DateTime DocumentDate,
        IReadOnlyList<SaleItemRequest> Items);

    private sealed record SaleItemRequest(string ProductId, decimal Quantity, decimal UnitPrice);

    private sealed record SaleCreatedResponse(string? Id, string? DocumentNumber);

    private sealed record ItemsResponse<T>(IReadOnlyList<T>? Items);

    private sealed record StockRow(string? WarehouseId, ProductRef? Product, decimal Quantity);

    private sealed record ProductRef(string? Id, string? Barcode);

    private sealed record ProductLookupResponse(ProductRef? Product);

    private sealed record ErrorResponse(string? Error);
}
