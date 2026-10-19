using Khazar.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Khazar.Application;

public static class DependencyInjection
{
    /// <summary>Registers the application use cases (catalog, checkout, authentication) consumed by the WebAPI controllers.</summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Scoped to match the repositories they depend on (one DbContext per request).
        services.AddScoped<IProductAppService, ProductAppService>();
        services.AddScoped<IOrderAppService, OrderAppService>();
        services.AddScoped<IAuthAppService, AuthAppService>();

        return services;
    }
}
