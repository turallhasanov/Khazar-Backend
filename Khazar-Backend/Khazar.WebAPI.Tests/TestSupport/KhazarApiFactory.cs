using Khazar.Infrastructure.Persistence;
using Khazar.WebAPI.Controllers.v1;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Khazar.WebAPI.Tests.TestSupport;

/// <summary>
/// Hosts the real Khazar API (Program.cs) in memory: the SQL Server DbContext becomes EF InMemory and the background
/// workers (NexusERP order sync, e-mail dispatch) are not started. Exception handling, status code pages, CORS and
/// authentication are production code.
/// </summary>
/// <remarks><see cref="ProductsController"/> stands in for the internal top-level Program class.</remarks>
public sealed class KhazarApiFactory : WebApplicationFactory<ProductsController>
{
    public const string AllowedOrigin = "http://localhost:5173";

    private readonly string _databaseName = "khazar-api-" + Guid.NewGuid();

    // Program.cs validates these while it builds the app (JWT secret length, SMTP vs log delivery), before
    // WebApplicationFactory's own configuration applies, so they are environment variables. Test-only values.
    static KhazarApiFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__KhazarDb", "Server=unused;Database=unused");
        Environment.SetEnvironmentVariable("Jwt__Secret", "khazar-integration-tests-only-signing-key-0123456789");
        Environment.SetEnvironmentVariable("Smtp__DeliveryMethod", "Log");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", AllowedOrigin);

        // Serilog's file sink (WriteTo index 1 in appsettings.json) writes to a temp folder, not Khazar.WebAPI/logs;
        // shared, since several test hosts log at once.
        Environment.SetEnvironmentVariable("Serilog__WriteTo__1__Args__configure__0__Args__path", Path.Combine(Path.GetTempPath(), "khazar-tests", "logs", "khazar-.txt"));
        Environment.SetEnvironmentVariable("Serilog__WriteTo__1__Args__configure__0__Args__shared", "true");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development: no migrations or seeding at startup and no exception details in 500 responses.
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<KhazarDbContext>>();
            services.RemoveAll<KhazarDbContext>();
            services.AddDbContext<KhazarDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            foreach (var worker in services.Where(d => d.ServiceType == typeof(IHostedService)
                         && d.ImplementationType?.Namespace == "Khazar.WebAPI.BackgroundJobs").ToList())
                services.Remove(worker);
        });
    }
}
