using Khazar.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Khazar.Infrastructure.ExternalServices;

public static class NexusErpServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="INexusErpClient"/> as a typed HttpClient managed by IHttpClientFactory, with settings
    /// bound from the "NexusErp" configuration section. Requests carry a JWT from <see cref="NexusErpTokenProvider"/>,
    /// which logs in through its own named client.
    /// </summary>
    public static IServiceCollection AddNexusErpClient(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<NexusErpOptions>(configuration.GetSection(NexusErpOptions.SectionName));

        // Singleton: the cached token is shared by every request.
        services.AddSingleton<NexusErpTokenProvider>();
        services.AddTransient<NexusErpAuthHandler>();

        services.AddHttpClient(NexusErpTokenProvider.HttpClientName, ApplyOptions);
        services.AddHttpClient<INexusErpClient, NexusErpClient>(ApplyOptions)
            .AddHttpMessageHandler<NexusErpAuthHandler>();

        return services;
    }

    private static void ApplyOptions(IServiceProvider serviceProvider, HttpClient httpClient) =>
        serviceProvider.GetRequiredService<IOptions<NexusErpOptions>>().Value.ApplyTo(httpClient);
}
