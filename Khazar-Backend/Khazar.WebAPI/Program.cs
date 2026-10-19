using System.Reflection;
using Khazar.Application;
using Khazar.Infrastructure;
using Khazar.Infrastructure.Persistence;
using Khazar.WebAPI.Cors;
using Khazar.WebAPI.ExceptionHandling;
using Khazar.WebAPI.HealthChecks;
using Khazar.WebAPI.Routing;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;

const string LiveTag = "live";
const string ReadyTag = "ready";

var builder = WebApplication.CreateBuilder(args);

// Fail at startup, in every environment, when a registration is missing or a scoped service
// is captured by a singleton, instead of on the first request that resolves it.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

// Composition root: each layer registers its own services.
// Application: product and order use cases.
builder.Services.AddApplicationServices();

// Infrastructure: DbContext, repositories and the NexusERP client.
builder.Services.AddInfrastructureServices(builder.Configuration);

// Default System.Text.Json settings use camelCase, matching the property names the React frontend reads.
builder.Services.AddControllers(options =>
    options.Conventions.Add(new RouteTokenTransformerConvention(new LowercaseParameterTransformer())));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Khazar Storefront API",
        Version = "v1",
        Description = "Catalog and checkout endpoints for the Khazar React storefront. " +
                      "Errors are returned as RFC 7807 ProblemDetails with a traceId for support requests.",
    });

    // Operation ids come from the C# method name without the "Async" suffix (GetProducts, CreateOrder, ...),
    // so Swagger UI and generated clients get stable, readable names.
    options.CustomOperationIds(api =>
        api.ActionDescriptor is ControllerActionDescriptor action ? TrimAsyncSuffix(action.MethodInfo.Name) : null);

    // Controller XML comments become operation descriptions in Swagger UI.
    var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlFile))
        options.IncludeXmlComments(xmlFile);
});

// Error handling: every error response is ProblemDetails JSON with a traceId.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions.TryAdd(
            "traceId", System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddFrontendCors(builder.Configuration);

// Health checks: "live" means the process is up, "ready" means it can serve traffic (database reachable).
builder.Services.Configure<SystemHealthCheckOptions>(builder.Configuration.GetSection(SystemHealthCheckOptions.SectionName));
builder.Services.AddHealthChecks()
    .AddCheck<SystemHealthCheck>("system", tags: [LiveTag])
    .AddCheck<DatabaseHealthCheck>("database", failureStatus: HealthStatus.Unhealthy, tags: [ReadyTag]);

var app = builder.Build();

// Outermost: converts exceptions thrown anywhere below into ProblemDetails.
app.UseExceptionHandler();

// Gives body-less error responses (unknown route 404, 405, 415) the same ProblemDetails shape.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.DocumentTitle = "Khazar Storefront API");

    await InitializeDatabaseAsync(app);
}

// CORS runs before the HTTPS redirect: a redirected preflight (OPTIONS) is rejected by browsers.
app.UseCors(FrontendCorsExtensions.PolicyName);
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

// Full report (system + database): 200 when Healthy or Degraded, 503 when Unhealthy.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
});

// Liveness only: never touches the database, so a database outage does not get the process restarted.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains(LiveTag),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
});

LogCorsConfiguration(app);

await app.RunAsync();

// Development only: creates the schema and seeds the catalog so the frontend has data to work with.
// EnsureCreated bypasses migrations; replace it with Database.MigrateAsync once migrations are added.
// A database failure is logged instead of stopping the API, so Swagger stays reachable for diagnosis.
static async Task InitializeDatabaseAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

    try
    {
        var context = scope.ServiceProvider.GetRequiredService<KhazarDbContext>();
        await context.Database.EnsureCreatedAsync();
        await KhazarDbInitializer.SeedAsync(context, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization failed; the API is running without a ready database.");
    }
}

static string TrimAsyncSuffix(string methodName) =>
    methodName.EndsWith("Async", StringComparison.Ordinal) ? methodName[..^"Async".Length] : methodName;

static void LogCorsConfiguration(WebApplication app)
{
    var origins = FrontendCorsExtensions.ReadAllowedOrigins(app.Configuration);

    if (origins.Length == 0)
        app.Logger.LogWarning(
            "No CORS origins are configured ({Key}); browsers will block cross-origin calls from the frontend.",
            FrontendCorsExtensions.AllowedOriginsKey);
    else
        app.Logger.LogInformation("CORS enabled for origins: {Origins}.", string.Join(", ", origins));
}
