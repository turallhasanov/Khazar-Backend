using Microsoft.Extensions.Configuration;

namespace Khazar.Infrastructure.Email;

/// <summary>Settings bound from the "Storefront" section: where email links point and what the welcome email offers.</summary>
public sealed class StorefrontOptions
{
    public const string SectionName = "Storefront";

    // Log delivery never sends a link to anyone, so local development may leave BaseUrl empty.
    private const string DevelopmentBaseUrl = "http://localhost:5173";

    /// <summary>Public URL of the React storefront, e.g. "https://khazar.az"; links in emails are built from it.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Discount code shown in the newsletter welcome email; leave empty to send the welcome without a code.</summary>
    public string NewsletterDiscountCode { get; set; } = string.Empty;

    /// <summary>Discount of <see cref="NewsletterDiscountCode"/> in percent, shown in the email; 0 hides the amount.</summary>
    public int NewsletterDiscountPercent { get; set; }

    /// <summary>Binds and validates the section; BaseUrl is required whenever real emails are sent.</summary>
    public static StorefrontOptions FromConfiguration(IConfiguration configuration, EmailDeliveryMethod deliveryMethod)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(SectionName).Get<StorefrontOptions>() ?? new StorefrontOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            if (deliveryMethod == EmailDeliveryMethod.Smtp)
                throw new InvalidOperationException(
                    $"Configuration value '{SectionName}:{nameof(BaseUrl)}' is required when emails are sent through SMTP: " +
                    "password reset and unsubscribe links point to the storefront.");

            options.BaseUrl = DevelopmentBaseUrl;
        }

        if (!Uri.TryCreate(options.BaseUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException(
                $"Configuration value '{SectionName}:{nameof(BaseUrl)}' must be an absolute http(s) URL, e.g. 'https://khazar.az'.");

        if (options.NewsletterDiscountPercent is < 0 or > 100)
            throw new InvalidOperationException(
                $"Configuration value '{SectionName}:{nameof(NewsletterDiscountPercent)}' must be between 0 and 100.");

        options.BaseUrl = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        options.NewsletterDiscountCode = options.NewsletterDiscountCode.Trim();
        return options;
    }

    /// <summary>Absolute storefront URL for a path such as "/sifre-yenile", with URL-encoded query parameters.</summary>
    public string Link(string path, params (string Name, string Value)[] query)
    {
        var url = BaseUrl + path;
        if (query.Length == 0)
            return url;

        return url + "?" + string.Join("&", query.Select(q => $"{Uri.EscapeDataString(q.Name)}={Uri.EscapeDataString(q.Value)}"));
    }
}
