using Khazar.Application.Common.Interfaces;
using Khazar.Infrastructure.Authentication;
using Khazar.Infrastructure.Email;
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

    /// <summary>Registers the DbContext, repositories, token issuing, email delivery and the NexusERP client.</summary>
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
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Settings are validated here, so a missing JWT secret or SMTP host fails at startup.
        services.AddSingleton(JwtOptions.FromConfiguration(configuration));
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        var smtpOptions = SmtpOptions.FromConfiguration(configuration);
        services.AddSingleton(smtpOptions);
        if (smtpOptions.DeliveryMethod == EmailDeliveryMethod.Log)
            services.AddSingleton<IEmailService, LogEmailService>();
        else
            services.AddSingleton<IEmailService, SmtpEmailService>();

        services.AddNexusErpClient(configuration);

        return services;
    }
}
