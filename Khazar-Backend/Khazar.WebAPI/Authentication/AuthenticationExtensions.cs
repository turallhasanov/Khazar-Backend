using System.Threading.RateLimiting;
using Khazar.Application.Common.Interfaces;
using Khazar.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Khazar.WebAPI.Authentication;

public static class AuthenticationExtensions
{
    /// <summary>Rate limit policy for the anonymous auth endpoints (login, register, code verification).</summary>
    public const string AuthRateLimitPolicy = "auth";

    // Per client IP: enough for a person retyping a password, too few to brute-force a 6-digit code or a password.
    private const int AuthRequestsPerWindow = 20;
    private static readonly TimeSpan AuthRateLimitWindow = TimeSpan.FromMinutes(1);

    /// <summary>Rate limit policy for public write endpoints (newsletter sign-up, review submission).</summary>
    public const string PublicFormRateLimitPolicy = "public-form";

    // Per client IP: a person submits these forms a few times at most; stops scripted list stuffing.
    private const int PublicFormRequestsPerWindow = 10;

    // Tolerated clock difference between token issuing and validation.
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Validates "Authorization: Bearer" access tokens issued by <see cref="JwtTokenGenerator"/>, using the same
    /// validated <see cref="JwtOptions"/> instance that Infrastructure registered.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtOptions>((options, jwt) =>
            {
                // Keep claim names as issued ("sub", "email") instead of mapping them to long WS-* URIs.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = ClockSkew,
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                };
            });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Limits auth and public form requests per client IP; rejected requests get 429 (as ProblemDetails via
    /// UseStatusCodePages).
    /// </summary>
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthRateLimitPolicy, context => PerIpLimiter(context, AuthRequestsPerWindow));
            options.AddPolicy(PublicFormRateLimitPolicy, context => PerIpLimiter(context, PublicFormRequestsPerWindow));
        });

        return services;
    }

    private static RateLimitPartition<string> PerIpLimiter(HttpContext context, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = AuthRateLimitWindow,
                QueueLimit = 0,
            });
}
