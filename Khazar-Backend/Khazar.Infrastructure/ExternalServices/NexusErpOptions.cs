namespace Khazar.Infrastructure.ExternalServices;

/// <summary>Settings bound from the "NexusErp" configuration section.</summary>
public sealed class NexusErpOptions
{
    public const string SectionName = "NexusErp";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Upper bound for a single ERP call; storefront requests must not hang on a slow ERP.</summary>
    public int TimeoutSeconds { get; set; } = 10;
}
