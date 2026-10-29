using Khazar.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Khazar.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the application use cases (catalog, categories, checkout, authentication, reviews, newsletter,
    /// storefront content, contact messages and order management) consumed by the
    /// WebAPI controllers.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Scoped to match the repositories they depend on (one DbContext per request).
        services.AddScoped<ICategoryAppService, CategoryAppService>();
        services.AddScoped<IAdminCategoryAppService, AdminCategoryAppService>();
        services.AddScoped<IProductAppService, ProductAppService>();
        services.AddScoped<IAdminProductAppService, AdminProductAppService>();
        services.AddScoped<IOrderAppService, OrderAppService>();
        services.AddScoped<IAuthAppService, AuthAppService>();
        services.AddScoped<IProductReviewAppService, ProductReviewAppService>();
        services.AddScoped<INewsletterAppService, NewsletterAppService>();
        services.AddScoped<IAdminNewsletterAppService, AdminNewsletterAppService>();
        services.AddScoped<IAdminOrderAppService, AdminOrderAppService>();
        services.AddScoped<ISiteContentAppService, SiteContentAppService>();
        services.AddScoped<IAdminContentAppService, AdminContentAppService>();
        services.AddScoped<IContactMessageAppService, ContactMessageAppService>();

        return services;
    }
}
