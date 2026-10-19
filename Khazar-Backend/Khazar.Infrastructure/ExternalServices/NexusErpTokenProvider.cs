using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Khazar.Infrastructure.ExternalServices;

/// <summary>
/// Logs in to NexusERP with the service account and caches the JWT until shortly before it expires.
/// Registered as a singleton, so every request shares one token; concurrent renewals are serialized,
/// so an expired token triggers exactly one login.
/// Login failures are thrown as <see cref="HttpRequestException"/>, which <see cref="NexusErpClient"/>
/// already treats as "NexusERP unavailable".
/// </summary>
public sealed class NexusErpTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<NexusErpOptions> options,
    ILogger<NexusErpTokenProvider> logger)
{
    /// <summary>Named HttpClient for the login call; it has no auth handler, so logging in cannot recurse.</summary>
    public const string HttpClientName = "NexusErp.Auth";

    private const string LoginEndpoint = "api/auth/login";

    // Renew this long before "exp", so a token cannot expire between being attached and being checked.
    private static readonly TimeSpan RenewBeforeExpiry = TimeSpan.FromMinutes(2);

    // Used when the token carries no readable "exp" claim: short enough that a wrong guess costs one extra login.
    private static readonly TimeSpan FallbackLifetime = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _renewLock = new(1, 1);
    private CachedToken? _cached;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        var cached = Volatile.Read(ref _cached);
        if (cached?.IsFresh == true)
            return cached.Value;

        await _renewLock.WaitAsync(cancellationToken);
        try
        {
            // Another request may have renewed the token while this one waited for the lock.
            cached = _cached;
            if (cached?.IsFresh == true)
                return cached.Value;

            cached = await LoginAsync(cancellationToken);
            Volatile.Write(ref _cached, cached);
            return cached.Value;
        }
        finally
        {
            _renewLock.Release();
        }
    }

    /// <summary>Forgets the token NexusERP rejected. A newer token fetched meanwhile by another request is kept.</summary>
    public void Invalidate(string rejectedToken)
    {
        var current = Volatile.Read(ref _cached);
        if (current is not null && current.Value == rejectedToken)
            Interlocked.CompareExchange(ref _cached, null, current);
    }

    private async Task<CachedToken> LoginAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var httpClient = httpClientFactory.CreateClient(HttpClientName);

        using var response = await httpClient.PostAsJsonAsync(
            LoginEndpoint, new LoginRequest(settings.Email, settings.Password), cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"NexusERP login for '{settings.Email}' failed with HTTP {(int)response.StatusCode}.",
                inner: null,
                response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload?.Token))
            throw new HttpRequestException("NexusERP login succeeded but returned no token.");

        // Company endpoints (sales, stock, products) answer 403 to a token without a company.
        if (string.IsNullOrWhiteSpace(payload.User?.CompanyId))
            logger.LogWarning(
                "NexusERP account {Email} belongs to no company; NexusERP will reject every sale and stock request.",
                settings.Email);

        var expiresAt = ReadExpiry(payload.Token) ?? DateTimeOffset.UtcNow + FallbackLifetime;
        logger.LogInformation("Logged in to NexusERP as {Email}; token valid until {ExpiresAt:O}.", settings.Email, expiresAt);

        return new CachedToken(payload.Token, expiresAt);
    }

    /// <summary>Reads the "exp" claim from the JWT payload. The signature is NexusERP's concern, not ours.</summary>
    private DateTimeOffset? ReadExpiry(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return null;

            // Base64url without padding -> standard Base64.
            var base64 = parts[1].Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');

            using var claims = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
            return claims.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            logger.LogWarning("NexusERP token has an unreadable payload; renewing it after {Minutes} minutes.", FallbackLifetime.TotalMinutes);
            return null;
        }
    }

    private sealed record CachedToken(string Value, DateTimeOffset ExpiresAt)
    {
        public bool IsFresh => DateTimeOffset.UtcNow < ExpiresAt - RenewBeforeExpiry;
    }

    private sealed record LoginRequest(string Email, string Password);

    private sealed record LoginResponse(string? Token, LoginUser? User);

    private sealed record LoginUser(string? CompanyId);
}
