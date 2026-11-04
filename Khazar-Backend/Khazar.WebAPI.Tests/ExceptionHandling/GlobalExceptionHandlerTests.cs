using System.Net;
using System.Text.Json.Nodes;
using Khazar.Application.Common.Exceptions;
using Khazar.Application.Services;
using Khazar.Domain.Exceptions;
using Khazar.WebAPI.Tests.TestSupport;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Khazar.WebAPI.Tests.ExceptionHandling;

/// <summary>
/// Khazar's GlobalExceptionHandler through the real pipeline: each exception type maps to its status and errorCode in
/// RFC 7807 form; an unexpected one is a 500 that reveals nothing. The storefront's apiFaults.js keys off these codes.
/// </summary>
public sealed class GlobalExceptionHandlerTests(KhazarApiFactory factory) : IClassFixture<KhazarApiFactory>
{
    /// <summary>A client whose GET /api/products throws <paramref name="exception"/>.</summary>
    private HttpClient ClientThrowing(Exception exception)
    {
        var products = new Mock<IProductAppService>();
        products.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>())).ThrowsAsync(exception);

        return factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IProductAppService>();
            services.AddScoped(_ => products.Object);
        })).CreateClient();
    }

    private static async Task<JsonObject> ProblemAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
    }

    [Fact]
    public async Task An_unexpected_exception_is_a_500_with_only_a_trace_id()
    {
        var response = await ClientThrowing(new InvalidOperationException("Server=PROD-SQL;Password=hunter2")).GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var problem = await ProblemAsync(response);
        problem["status"]!.GetValue<int>().Should().Be(500);
        problem["title"]!.GetValue<string>().Should().Be("An unexpected error occurred");
        problem["errorCode"]!.GetValue<string>().Should().Be("internal_error");
        problem["traceId"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        problem["instance"]!.GetValue<string>().Should().Be("GET /api/products");
        problem.ContainsKey("exception").Should().BeFalse();
        problem.ToJsonString().Should().NotContain("hunter2").And.NotContain("PROD-SQL");
    }

    public static TheoryData<Exception, HttpStatusCode, string> MappedExceptions => new()
    {
        { new ValidationException("Ad tələb olunur.", "name_required"), HttpStatusCode.BadRequest, "name_required" },
        { new NotFoundException("Product", "x"), HttpStatusCode.NotFound, "not_found" },
        { new ConflictException("Slug artıq var."), HttpStatusCode.Conflict, "conflict" },
        { new InsufficientStockException("KHZ-1"), HttpStatusCode.Conflict, "insufficient_stock" },
        { new DomainException("Qayda pozuldu."), HttpStatusCode.BadRequest, "business_rule_violation" },
        { new ExternalServiceException("NexusERP sinxronizasiyası alınmadı.", "nexus_sync_failed"), HttpStatusCode.BadGateway, "nexus_sync_failed" },
        { new TimeoutException("db"), HttpStatusCode.ServiceUnavailable, "service_unavailable" },
    };

    [Theory]
    [MemberData(nameof(MappedExceptions))]
    public async Task Known_exceptions_keep_their_status_and_error_code(Exception exception, HttpStatusCode status, string errorCode)
    {
        var response = await ClientThrowing(exception).GetAsync("/api/products");

        response.StatusCode.Should().Be(status);
        var problem = await ProblemAsync(response);
        problem["errorCode"]!.GetValue<string>().Should().Be(errorCode);
        problem["traceId"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();

        // Client-facing messages of our own exceptions are shown; a 5xx never shows the exception's message.
        if ((int)status < 500 || exception is ExternalServiceException)
            problem["detail"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Insufficient_stock_names_the_sku()
    {
        var problem = await ProblemAsync(await ClientThrowing(new InsufficientStockException("KHZ-COAT-BLK-M")).GetAsync("/api/products"));

        problem["sku"]!.GetValue<string>().Should().Be("KHZ-COAT-BLK-M");
    }

    [Fact]
    public async Task The_500_keeps_CORS_headers_for_the_storefront_origin()
    {
        var client = ClientThrowing(new InvalidOperationException("boom"));
        client.DefaultRequestHeaders.Add("Origin", KhazarApiFactory.AllowedOrigin);

        var response = await client.GetAsync("/api/products");

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal(KhazarApiFactory.AllowedOrigin);
    }

    [Fact]
    public async Task An_unknown_route_is_a_ProblemDetails_404()
    {
        var response = await factory.CreateClient().GetAsync("/api/no-such-endpoint");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProblemAsync(response))["status"]!.GetValue<int>().Should().Be(404);
    }

    [Fact]
    public async Task A_missing_product_is_a_404_problem_without_any_exception()
    {
        var response = await factory.CreateClient().GetAsync($"/api/products/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        // Controller-made problems follow BaseApiController's [Produces("application/json")]; same ProblemDetails fields.
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        problem["status"]!.GetValue<int>().Should().Be(404);
        problem["traceId"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_catalog_works_on_the_in_memory_database()
    {
        var response = await factory.CreateClient().GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray().Should().BeEmpty();
    }
}
