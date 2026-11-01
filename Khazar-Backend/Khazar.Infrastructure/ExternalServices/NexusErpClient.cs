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
    private const string ProductsEndpoint = "api/products";
    private const string CategoriesEndpoint = "api/categories";
    private const string UnitsEndpoint = "api/units";
    private const string StoresEndpoint = "api/stores";
    // NexusERP answers 403 to every company-scoped call when the login belongs to no company (e.g. the demo account).
    private const string NoCompanyMessage =
        "NexusERP 403 qaytardı: 'NexusErp:Email' hesabı heç bir şirkətə bağlı deyil. Şirkətə bağlı istifadəçi (məs. Khazar şirkətinin admini) göstərin.";
    private const string DefaultUnitName = "Ədəd";
    private const string DefaultUnitShortName = "əd.";
    private const string ProductByBarcodeEndpoint = "api/products/barcode/";
    // Retail (B2C) orders: NexusERP books them to its "Khazar Online Müştəri" account, never to wholesale sales.
    private const string OnlineOrdersEndpoint = "api/online-orders";

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

    public async Task<ErpPushResult> PushOrderToErpAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.Items.Count == 0)
            return Fail(order, "sifarişdə məhsul yoxdur (məhsullar yüklənməyib?).");

        // Order items snapshot names and prices, not SKUs; the variant still holds the SKU and what a missing
        // NexusERP product is created from.
        var variantIds = order.Items.Select(i => i.VariantId).Distinct().ToList();
        var variantsById = await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => new VariantInfo(
                v.Id, v.SKU, v.Size, v.Color, v.Product!.Name, v.Product.Price, v.Product.Category.Name))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        try
        {
            var storeId = await ResolveStoreIdAsync(cancellationToken);
            if (storeId.Error is not null)
                return Fail(order, storeId.Error);

            var productIdBySku = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = new List<SaleItemRequest>(order.Items.Count);

            foreach (var item in order.Items)
            {
                if (!variantsById.TryGetValue(item.VariantId, out var variant))
                    return Fail(order, $"variant {item.VariantId} artıq mövcud deyil, SKU bilinmir ({item.ProductName}).");

                if (!productIdBySku.TryGetValue(variant.Sku, out var productId))
                {
                    productId = await FindProductIdAsync(variant.Sku, cancellationToken);

                    if (productId is null)
                    {
                        // Not in NexusERP yet (e.g. added in Khazar after the last catalog integration): create it,
                        // then the order can go through.
                        var created = await CreateMissingProductAsync(order, variant, cancellationToken);
                        if (created.Error is not null)
                            return Fail(order, created.Error);

                        productId = created.ProductId!;
                    }

                    productIdBySku[variant.Sku] = productId;
                }

                // Storefront price, so the ERP document matches what the customer paid.
                lines.Add(new SaleItemRequest(productId, item.Quantity, item.UnitPrice));
            }

            // The order number makes the push idempotent: NexusERP returns the existing sale when a retry resends it.
            var request = new CreateOnlineOrderRequest(
                order.OrderNumber,
                storeId.Id!,
                WarehouseId,
                order.CreatedAt,
                new RetailCustomer(order.CustomerName, order.CustomerEmail, order.ShippingAddress.ToString()),
                lines);

            using var response = await _httpClient.PostAsJsonAsync(OnlineOrdersEndpoint, request, cancellationToken);

            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
            {
                var sale = await response.Content.ReadFromJsonAsync<SaleCreatedResponse>(cancellationToken);
                _logger.LogInformation(
                    "Order {OrderNumber} was accepted by NexusERP as sale {DocumentNumber}.",
                    order.OrderNumber, sale?.DocumentNumber);
                return ErpPushResult.Ok;
            }

            var error = await ReadErrorAsync(response, cancellationToken);
            return Fail(order, $"NexusERP sifarişi rədd etdi (HTTP {(int)response.StatusCode}): {error ?? "səbəb göstərilməyib"}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            return Fail(order, NoCompanyMessage);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            // NexusERP refused the service account, or no token could be obtained (see the NexusErpTokenProvider warning).
            return Fail(order, $"NexusERP girişi uğursuz oldu (HTTP 401): '{NexusErpOptions.SectionName}:Email' və '{NexusErpOptions.SectionName}:Password' NexusERP-dəki istifadəçiyə uyğun olmalıdır.");
        }
        catch (Exception ex) when (IsTransportFailure(ex, cancellationToken))
        {
            LogTransportFailure(ex, "order push for " + order.OrderNumber);
            return ErpPushResult.Failed($"NexusERP ilə əlaqə qurulmadı: {ex.Message}");
        }
    }

    public async Task<ErpPushResult> CancelOrderInErpAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNumber);

        try
        {
            using var response = await _httpClient.DeleteAsync(
                OnlineOrdersEndpoint + "/" + Uri.EscapeDataString(orderNumber), cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Order {OrderNumber} was cancelled in NexusERP.", orderNumber);
                return ErpPushResult.Ok;
            }

            var error = await ReadErrorAsync(response, cancellationToken);

            // NexusERP answers 404 with its own error body when it never received the order, so there is nothing to
            // cancel there. A 404 without that body means the route is missing (an older NexusERP build): a failure.
            if (response.StatusCode == HttpStatusCode.NotFound && error is not null)
            {
                _logger.LogWarning("NexusERP has no sale for order {OrderNumber}; nothing to cancel there.", orderNumber);
                return ErpPushResult.Ok;
            }

            var reason = $"NexusERP sifarişi ləğv etmədi (HTTP {(int)response.StatusCode}): {error ?? "səbəb göstərilməyib"}";
            _logger.LogWarning("Order {OrderNumber} was not cancelled in NexusERP: {Reason}", orderNumber, reason);
            return ErpPushResult.Failed(reason);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return ErpPushResult.Failed($"NexusERP girişi uğursuz oldu (HTTP 401): '{NexusErpOptions.SectionName}:Email' və '{NexusErpOptions.SectionName}:Password' NexusERP-dəki istifadəçiyə uyğun olmalıdır.");
        }
        catch (Exception ex) when (IsTransportFailure(ex, cancellationToken))
        {
            LogTransportFailure(ex, "order cancellation for " + orderNumber);
            return ErpPushResult.Failed($"NexusERP ilə əlaqə qurulmadı: {ex.Message}");
        }
    }

    private ErpPushResult Fail(Order order, string reason)
    {
        _logger.LogWarning("Order {OrderNumber} was not pushed to NexusERP: {Reason}", order.OrderNumber, reason);
        return ErpPushResult.Failed(reason);
    }

    /// <summary>Creates the variant's product in NexusERP (barcode = SKU) with its category and unit as needed.</summary>
    private async Task<(string? ProductId, string? Error)> CreateMissingProductAsync(
        Order order, VariantInfo variant, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Order {OrderNumber}: NexusERP has no product with barcode {Sku} ({Name}); creating it.",
            order.OrderNumber, variant.Sku, variant.DisplayName);

        var categoryId = await FindOrCreateAsync(CategoriesEndpoint, variant.CategoryName,
            new { name = variant.CategoryName }, cancellationToken);
        if (categoryId.Error is not null)
            return (null, $"{variant.Sku}: kateqoriya yaradıla bilmədi — {categoryId.Error}");

        var unitId = await FindOrCreateAsync(UnitsEndpoint, DefaultUnitName,
            new { name = DefaultUnitName, shortName = DefaultUnitShortName }, cancellationToken, fallbackToFirst: true);
        if (unitId.Error is not null)
            return (null, $"{variant.Sku}: ölçü vahidi yaradıla bilmədi — {unitId.Error}");

        using var response = await _httpClient.PostAsJsonAsync(ProductsEndpoint, new
        {
            categoryId = categoryId.Id,
            unitId = unitId.Id,
            name = variant.DisplayName,
            barcode = variant.Sku,
            salePrice = variant.Price
        }, cancellationToken);

        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
        {
            var created = await response.Content.ReadFromJsonAsync<IdResponse>(cancellationToken);
            if (!string.IsNullOrWhiteSpace(created?.Id))
            {
                _logger.LogInformation("NexusERP product {Sku} ({Name}) was created for order {OrderNumber}.",
                    variant.Sku, variant.DisplayName, order.OrderNumber);
                return (created.Id, null);
            }
        }

        // Created by someone else between the lookup and the POST: use theirs.
        if (response.StatusCode == HttpStatusCode.Conflict && await FindProductIdAsync(variant.Sku, cancellationToken) is { } existing)
            return (existing, null);

        return (null, $"{variant.Sku}: məhsul NexusERP-də yaradıla bilmədi (HTTP {(int)response.StatusCode}): "
            + (await ReadErrorAsync(response, cancellationToken) ?? "səbəb göstərilməyib"));
    }

    /// <summary>Id of the named category/unit in NexusERP, created when missing. Units may fall back to any existing one.</summary>
    private async Task<(string? Id, string? Error)> FindOrCreateAsync(
        string endpoint, string name, object createBody, CancellationToken cancellationToken, bool fallbackToFirst = false)
    {
        using (var list = await _httpClient.GetAsync(endpoint, cancellationToken))
        {
            if (!list.IsSuccessStatusCode)
                return (null, $"{endpoint} HTTP {(int)list.StatusCode}");

            var rows = (await list.Content.ReadFromJsonAsync<ItemsResponse<NamedRow>>(cancellationToken))?.Items ?? [];
            var match = rows.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? (fallbackToFirst ? rows.FirstOrDefault() : null);

            if (match?.Id is not null)
                return (match.Id, null);
        }

        using var response = await _httpClient.PostAsJsonAsync(endpoint, createBody, cancellationToken);

        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
        {
            var created = await response.Content.ReadFromJsonAsync<IdResponse>(cancellationToken);
            if (!string.IsNullOrWhiteSpace(created?.Id))
                return (created.Id, null);
        }

        return (null, $"HTTP {(int)response.StatusCode}: {await ReadErrorAsync(response, cancellationToken) ?? "səbəb göstərilməyib"}");
    }

    /// <summary>
    /// The configured store, or the first active NexusERP store when none is configured (an unset StoreId must not
    /// leave every order Pending). NexusERP identifies stores by id, so a made-up value could never match.
    /// </summary>
    private async Task<(string? Id, string? Error)> ResolveStoreIdAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_settings.StoreId))
            return (_settings.StoreId.Trim(), null);

        using var response = await _httpClient.GetAsync(StoresEndpoint, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return (null, NoCompanyMessage);

        if (!response.IsSuccessStatusCode)
            return (null, $"'{NexusErpOptions.SectionName}:StoreId' təyin olunmayıb və mağazalar oxunmadı (HTTP {(int)response.StatusCode}).");

        var stores = (await response.Content.ReadFromJsonAsync<ItemsResponse<StoreRow>>(cancellationToken))?.Items ?? [];
        var store = stores.FirstOrDefault(s => s.IsActive && !string.IsNullOrWhiteSpace(s.Id));

        if (store is null)
            return (null, $"'{NexusErpOptions.SectionName}:StoreId' təyin olunmayıb və NexusERP-də aktiv mağaza yoxdur. Əvvəlcə mağaza yaradın.");

        _logger.LogWarning(
            "'{Section}:StoreId' is not configured; using NexusERP store {StoreName} ({StoreId}). Set it to make this explicit.",
            NexusErpOptions.SectionName, store.Name, store.Id);
        return (store.Id, null);
    }

    public async Task<IReadOnlyList<NexusProductSnapshot>?> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var productsResponse = await _httpClient.GetAsync(ProductsEndpoint, cancellationToken);
            using var stocksResponse = await _httpClient.GetAsync(StocksEndpoint, cancellationToken);

            if (!productsResponse.IsSuccessStatusCode || !stocksResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "NexusERP catalog pull failed: products HTTP {ProductsStatus}, stocks HTTP {StocksStatus}.",
                    (int)productsResponse.StatusCode, (int)stocksResponse.StatusCode);
                return null;
            }

            var products = await productsResponse.Content.ReadFromJsonAsync<ItemsResponse<ProductRow>>(cancellationToken);
            var stocks = await stocksResponse.Content.ReadFromJsonAsync<ItemsResponse<StockRow>>(cancellationToken);
            if (products?.Items is null || stocks?.Items is null)
            {
                _logger.LogWarning("NexusERP returned an invalid products or stock payload; catalog pull skipped.");
                return null;
            }

            var stockByProductId = stocks.Items
                .Where(r => r.Product?.Id is not null && (WarehouseId is null || r.WarehouseId == WarehouseId))
                .GroupBy(r => r.Product!.Id!)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));

            return products.Items
                .Where(p => p.IsActive && !string.IsNullOrWhiteSpace(p.Barcode) && !string.IsNullOrWhiteSpace(p.Name))
                .Select(p => new NexusProductSnapshot(
                    p.Barcode!.Trim().ToUpperInvariant(),
                    p.Name!.Trim(),
                    string.IsNullOrWhiteSpace(p.Category?.Name) ? null : p.Category.Name.Trim(),
                    p.SalePrice,
                    // Fractional quantities (kg, m) are floored: the storefront sells whole units.
                    (int)Math.Max(0, Math.Floor(p.Id is not null && stockByProductId.TryGetValue(p.Id, out var qty) ? qty : 0))))
                .ToList();
        }
        catch (Exception ex) when (IsTransportFailure(ex, cancellationToken))
        {
            LogTransportFailure(ex, "catalog pull");
            return null;
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

    // Contract of NexusERP's POST api/online-orders (operation OnlineOrders_Create) and the responses read here.
    private sealed record CreateOnlineOrderRequest(
        string ExternalOrderNumber,
        string StoreId,
        string? WarehouseId,
        DateTime DocumentDate,
        RetailCustomer Customer,
        IReadOnlyList<SaleItemRequest> Items);

    // Khazar keeps one name field and no phone yet; NexusERP also accepts firstName/lastName and phone.
    private sealed record RetailCustomer(string FullName, string Email, string Address);

    private sealed record SaleItemRequest(string ProductId, decimal Quantity, decimal UnitPrice);

    private sealed record SaleCreatedResponse(string? Id, string? DocumentNumber);

    private sealed record ItemsResponse<T>(IReadOnlyList<T>? Items);

    private sealed record StockRow(string? WarehouseId, ProductRef? Product, decimal Quantity);

    private sealed record ProductRow(string? Id, string? Name, string? Barcode, decimal SalePrice, bool IsActive, CategoryRef? Category);

    private sealed record CategoryRef(string? Name);

    private sealed record ProductRef(string? Id, string? Barcode);

    private sealed record ProductLookupResponse(ProductRef? Product);

    private sealed record ErrorResponse(string? Error);

    private sealed record NamedRow(string? Id, string? Name);

    private sealed record StoreRow(string? Id, string? Name, bool IsActive);

    private sealed record IdResponse(string? Id);

    /// <summary>A Khazar variant with what a missing NexusERP product is created from.</summary>
    private sealed record VariantInfo(
        Guid Id, string Sku, string Size, string Color, string ProductName, decimal Price, string CategoryName)
    {
        // Same naming as NexusERP's catalog integration: single-variant products keep the plain name.
        public string DisplayName
        {
            get
            {
                var parts = new[] { Color, Size }.Where(p => !string.IsNullOrWhiteSpace(p)
                    && !p.Equals("Standart", StringComparison.OrdinalIgnoreCase)).ToList();
                var name = parts.Count == 0 ? ProductName : $"{ProductName} ({string.Join(" / ", parts)})";
                return name.Length > 200 ? name[..200] : name;
            }
        }
    }
}
