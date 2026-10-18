using Khazar.Application.Services;
using Khazar.Infrastructure;
using Khazar.Infrastructure.Persistence;
using Khazar.WebAPI.ExceptionHandling;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

// Application layer use cases.
builder.Services.AddScoped<IProductAppService, ProductAppService>();
builder.Services.AddScoped<IOrderAppService, OrderAppService>();

// Persistence, repositories and the NexusERP client.
builder.Services.AddInfrastructure(builder.Configuration);

// Default System.Text.Json settings use camelCase, matching the property names the React frontend reads.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("Location")));

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    await InitializeDatabaseAsync(app);
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.UseAuthorization();
app.MapControllers();

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
