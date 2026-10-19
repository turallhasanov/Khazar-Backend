namespace Khazar.WebAPI.Cors;

/// <summary>
/// CORS policy for the React storefront. Origins come from "Cors:AllowedOrigins" and are validated at startup:
/// a CORS origin must match the browser's Origin header exactly, so a typo or a trailing slash
/// ("http://localhost:5173/") would otherwise fail silently in the browser.
/// </summary>
public static class FrontendCorsExtensions
{
    public const string PolicyName = "Frontend";
    public const string AllowedOriginsKey = "Cors:AllowedOrigins";

    // Lets the browser reuse a successful preflight instead of sending OPTIONS before every POST.
    private static readonly TimeSpan PreflightMaxAge = TimeSpan.FromMinutes(10);

    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = ReadAllowedOrigins(configuration);

        services.AddCors(options =>
            options.AddPolicy(PolicyName, policy => policy
                .WithOrigins(origins)
                .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Options)
                // Authorization carries the JWT of a signed-in customer.
                .WithHeaders("Content-Type", "Accept", "Authorization")
                // Location carries the URL of a newly created order; Retry-After accompanies 429 responses.
                // Browsers hide response headers unless they are exposed.
                .WithExposedHeaders("Location", "Retry-After")
                .SetPreflightMaxAge(PreflightMaxAge)));

        return services;
    }

    /// <summary>Returns the normalized origins; throws on malformed entries so misconfiguration fails at startup.</summary>
    public static string[] ReadAllowedOrigins(IConfiguration configuration)
    {
        var configured = configuration.GetSection(AllowedOriginsKey).Get<string[]>() ?? [];

        return configured
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(NormalizeOrigin)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeOrigin(string origin)
    {
        var value = origin.Trim().TrimEnd('/');

        if (value == "*")
            throw new InvalidOperationException(
                $"'{AllowedOriginsKey}' must list explicit origins; the wildcard '*' is not allowed.");

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query))
        {
            throw new InvalidOperationException(
                $"'{AllowedOriginsKey}' contains an invalid origin '{origin}'. " +
                "Use scheme, host and optional port only, e.g. 'http://localhost:5173'.");
        }

        // Uri.GetLeftPart lower-cases the host and drops default ports, matching how browsers send Origin.
        return uri.GetLeftPart(UriPartial.Authority);
    }
}
