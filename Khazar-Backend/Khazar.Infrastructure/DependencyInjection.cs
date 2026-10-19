using Khazar.Application.Common.Interfaces;
using Khazar.Infrastructure.ExternalServices;
using Khazar.Infrastructure.Persistence;
using Khazar.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Khazar.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "KhazarDb";

    /// <summary>Registers the DbContext, repositories and the NexusERP client.</summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<KhazarDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        services.AddNexusErpClient(configuration);

        return services;
    }
}
