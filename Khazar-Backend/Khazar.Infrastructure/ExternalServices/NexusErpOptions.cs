namespace Khazar.Infrastructure.ExternalServices;

/// <summary>Settings bound from the "NexusErp" configuration section.</summary>
public sealed class NexusErpOptions
{
    public const string SectionName = "NexusErp";

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Service account NexusERP logs in with (POST api/auth/login). It must belong to a company.</summary>
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>NexusERP store that storefront orders are recorded under; POST api/sales requires one.</summary>
    public string StoreId { get; set; } = string.Empty;

    /// <summary>
    /// Optional. Warehouse for sales and stock lookups; empty means the store's own warehouse for sales
    /// and the total over all warehouses for stock.
    /// </summary>
    public string? WarehouseId { get; set; }

    /// <summary>Upper bound for a single ERP call; storefront requests must not hang on a slow ERP.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Validates the settings and applies BaseAddress and Timeout to a NexusERP HttpClient.</summary>
    internal void ApplyTo(HttpClient httpClient)
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri))
            throw new InvalidOperationException(
                $"Configuration value '{SectionName}:{nameof(BaseUrl)}' must be an absolute URL.");

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            throw new InvalidOperationException(
                $"Configuration values '{SectionName}:{nameof(Email)}' and '{SectionName}:{nameof(Password)}' are required.");

        if (TimeoutSeconds <= 0)
            throw new InvalidOperationException(
                $"Configuration value '{SectionName}:{nameof(TimeoutSeconds)}' must be positive.");

        // A trailing slash is required, otherwise relative endpoints replace the last path segment of the base URL.
        httpClient.BaseAddress = baseUri.AbsoluteUri.EndsWith('/') ? baseUri : new Uri(baseUri.AbsoluteUri + "/");
        httpClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
    }
}
