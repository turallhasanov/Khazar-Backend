using System.Net;
using System.Net.Http.Json;
using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Khazar.Infrastructure.ExternalServices;

/// <summary>
/// Typed HttpClient for NexusERP. The HttpClient is created by IHttpClientFactory, so connection pooling
/// and DNS refresh are handled by the factory. BaseAddress, the API key header and the timeout are applied
/// here from <see cref="NexusErpOptions"/>.
/// Every transport failure (connection refused, DNS failure, timeout, malformed response) is logged and turned
/// into the fallback return value; only cancellation requested by the caller is rethrown.
/// </summary>
public sealed class NexusErpClient : INexusErpClient
{
    public const string ApiKeyHeaderName = "X-Api-Key";

    private const string StockEndpoint = "api/v1/stock/";
    private const string OrdersEndpoint = "api/v1/orders";

    private readonly HttpClient _httpClient;
    private readonly ILogger<NexusErpClient> _logger;

    public NexusErpClient(HttpClient httpClient, IOptions<NexusErpOptions> options, ILogger<NexusErpClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _logger = logger;

        var settings = options.Value;

        if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri))
            throw new InvalidOperationException(
                $"Configuration value '{NexusErpOptions.SectionName}:{nameof(NexusErpOptions.BaseUrl)}' must be an absolute URL.");

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException(
                $"Configuration value '{NexusErpOptions.SectionName}:{nameof(NexusErpOptions.ApiKey)}' is required.");

        if (settings.TimeoutSeconds <= 0)
            throw new InvalidOperationException(
                $"Configuration value '{NexusErpOptions.SectionName}:{nameof(NexusErpOptions.TimeoutSeconds)}' must be positive.");

        // A trailing slash is required, otherwise relative endpoints replace the last path segment of the base URL.
        _httpClient.BaseAddress = baseUri.AbsoluteUri.EndsWith('/') ? baseUri : new Uri(baseUri.AbsoluteUri + "/");
        _httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Remove(ApiKeyHeaderName);
        _httpClient.DefaultRequestHeaders.Add(ApiKeyHeaderName, settings.ApiKey);
    }

    public async Task<int?> GetStockBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU is required.", nameof(sku));

        // SKUs are stored upper-case by ProductVariant.
        var normalizedSku = sku.Trim().ToUpperInvariant();

        try
        {
            using var response = await _httpClient.GetAsync(
                StockEndpoint + Uri.EscapeDataString(normalizedSku), cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("NexusERP does not know SKU {Sku}; live stock is unavailable.", normalizedSku);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "NexusERP stock request for SKU {Sku} failed with HTTP {StatusCode}; falling back to local stock.",
                    normalizedSku, (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<StockResponse>(cancellationToken);
            if (payload is null || payload.Quantity < 0)
            {
                _logger.LogWarning(
                    "NexusERP returned an invalid stock payload for SKU {Sku}; falling back to local stock.", normalizedSku);
                return null;
            }

            return payload.Quantity;
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
            _logger.LogWarning(
                "Order {OrderNumber} is being pushed to NexusERP without items; were the items loaded?", order.OrderNumber);

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                OrdersEndpoint, ErpOrderRequest.From(order), cancellationToken);

            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
            {
                _logger.LogInformation("Order {OrderNumber} was accepted by NexusERP.", order.OrderNumber);
                return true;
            }

            _logger.LogWarning(
                "NexusERP rejected order {OrderNumber} with HTTP {StatusCode}.",
                order.OrderNumber, (int)response.StatusCode);
            return false;
        }
        catch (Exception ex) when (IsTransportFailure(ex, cancellationToken))
        {
            LogTransportFailure(ex, "order push for " + order.OrderNumber);
            return false;
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

    private sealed record StockResponse(string? Sku, int Quantity);

    private sealed record ErpOrderRequest(
        Guid OrderId,
        string OrderNumber,
        string CustomerName,
        string CustomerEmail,
        decimal TotalAmount,
        string Status,
        ErpAddress ShippingAddress,
        IReadOnlyList<ErpOrderItem> Items,
        DateTime CreatedAt)
    {
        public static ErpOrderRequest From(Order order) => new(
            order.Id,
            order.OrderNumber,
            order.CustomerName,
            order.CustomerEmail,
            order.TotalAmount,
            order.Status.ToString(),
            new ErpAddress(
                order.ShippingAddress.Street,
                order.ShippingAddress.City,
                order.ShippingAddress.ZipCode,
                order.ShippingAddress.Country),
            order.Items
                .Select(i => new ErpOrderItem(
                    i.ProductId, i.VariantId, i.ProductName, i.Size, i.Color, i.UnitPrice, i.Quantity, i.LineTotal))
                .ToList(),
            order.CreatedAt);
    }

    private sealed record ErpAddress(string Street, string City, string ZipCode, string Country);

    private sealed record ErpOrderItem(
        Guid ProductId,
        Guid VariantId,
        string ProductName,
        string Size,
        string Color,
        decimal UnitPrice,
        int Quantity,
        decimal LineTotal);
}
