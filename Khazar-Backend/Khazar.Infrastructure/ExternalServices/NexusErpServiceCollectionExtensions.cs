using Khazar.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Khazar.Infrastructure.ExternalServices;

public static class NexusErpServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="INexusErpClient"/> as a typed HttpClient managed by IHttpClientFactory,
    /// with settings bound from the "NexusErp" configuration section.
    /// </summary>
    public static IServiceCollection AddNexusErpClient(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<NexusErpOptions>(configuration.GetSection(NexusErpOptions.SectionName));
        services.AddHttpClient<INexusErpClient, NexusErpClient>();

        return services;
    }
}
