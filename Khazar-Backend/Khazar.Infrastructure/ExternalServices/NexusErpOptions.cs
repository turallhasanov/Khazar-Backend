namespace Khazar.Infrastructure.ExternalServices;

/// <summary>Settings bound from the "NexusErp" configuration section.</summary>
public sealed class NexusErpOptions
{
    public const string SectionName = "NexusErp";

    /// <summary>When false, placed orders are not pushed to NexusERP and stay Pending until it is enabled.</summary>
    public bool SyncEnabled { get; set; } = true;

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Service account NexusERP logs in with (POST api/auth/login). It must belong to a company.</summary>
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>NexusERP store that storefront orders are recorded under; POST api/online-orders requires one.</summary>
    public string StoreId { get; set; } = string.Empty;

    /// <summary>
    /// Optional. Warehouse for sales and stock lookups; empty means the store's own warehouse for sales
    /// and the total over all warehouses for stock.
    /// </summary>
    public string? WarehouseId { get; set; }

    /// <summary>Cover image of products the catalog pull adds, until an admin uploads real photos.</summary>
    public string PlaceholderImageUrl { get; set; } = "/favicon.svg";

    /// <summary>Upper bound for a single ERP call; storefront requests must not hang on a slow ERP.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Describes what is wrong with the settings, or null when they are usable. Checked when NexusERP is actually
    /// called (see <see cref="NexusErpTokenProvider"/>), never when the client is created: the client is a dependency
    /// of pages that do not talk to NexusERP (e.g. the admin order list), which must work with the ERP unconfigured.
    /// </summary>
    internal string? Validate()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
            return $"'{SectionName}:{nameof(BaseUrl)}' düzgün mütləq URL olmalıdır.";

        if (TimeoutSeconds <= 0)
            return $"'{SectionName}:{nameof(TimeoutSeconds)}' müsbət olmalıdır.";

        return null;
    }

    /// <summary>Applies BaseAddress and Timeout to a NexusERP HttpClient; settings that are not valid yet are skipped.</summary>
    internal void ApplyTo(HttpClient httpClient)
    {
        if (Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri))
            // A trailing slash is required, otherwise relative endpoints replace the last path segment of the base URL.
            httpClient.BaseAddress = baseUri.AbsoluteUri.EndsWith('/') ? baseUri : new Uri(baseUri.AbsoluteUri + "/");

        if (TimeoutSeconds > 0)
            httpClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
    }
}
