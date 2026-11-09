using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Khazar.Infrastructure.ExternalServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Khazar.WebAPI.Tests.NexusErp;

/// <summary>POST api/auth/login and the bearer token on every NexusERP call (NexusErpTokenProvider + NexusErpAuthHandler).</summary>
public sealed class NexusErpAuthenticationTests
{
    private readonly StubNexusErp _nexus = new();

    private NexusErpTokenProvider Provider(string email = "khazar@test.az", string password = "Secret-1!")
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(NexusErpTokenProvider.HttpClientName))
            .Returns(() => new HttpClient(_nexus, disposeHandler: false) { BaseAddress = new Uri("http://nexus.test/") });

        return new NexusErpTokenProvider(factory.Object,
            Options.Create(new NexusErpOptions { BaseUrl = "http://nexus.test", Email = email, Password = password }),
            NullLogger<NexusErpTokenProvider>.Instance);
    }

    private static string Jwt(DateTimeOffset expires, string id = "1")
    {
        static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{Encode("""{"alg":"HS256","typ":"JWT"}""")}.{Encode($$"""{"sub":"{{id}}","exp":{{expires.ToUnixTimeSeconds()}}}""")}.signature";
    }

    private static string LoginBody(string token, string? companyId = "co-1") =>
        new JsonObject { ["ok"] = true, ["token"] = token, ["user"] = new JsonObject { ["id"] = "u-1", ["companyId"] = companyId } }.ToJsonString();

    [Fact]
    public async Task Login_sends_the_service_account_and_caches_the_token_until_it_nears_expiry()
    {
        var token = Jwt(DateTimeOffset.UtcNow.AddHours(1));
        _nexus.On(HttpMethod.Post, "api/auth/login", HttpStatusCode.OK, LoginBody(token));
        var provider = Provider();

        (await provider.GetTokenAsync(CancellationToken.None)).Should().Be(token);
        (await provider.GetTokenAsync(CancellationToken.None)).Should().Be(token);

        var login = _nexus.To(HttpMethod.Post, "api/auth/login").Should().ContainSingle().Subject;
        var body = JsonNode.Parse(login.Body!)!;
        body["email"]!.GetValue<string>().Should().Be("khazar@test.az");
        body["password"]!.GetValue<string>().Should().Be("Secret-1!");
    }

    [Fact]
    public async Task A_token_about_to_expire_is_renewed()
    {
        _nexus.On(HttpMethod.Post, "api/auth/login", HttpStatusCode.OK, LoginBody(Jwt(DateTimeOffset.UtcNow.AddMinutes(1))));
        var provider = Provider();

        await provider.GetTokenAsync(CancellationToken.None);
        await provider.GetTokenAsync(CancellationToken.None);

        _nexus.To(HttpMethod.Post, "api/auth/login").Should().HaveCount(2);
    }

    [Fact]
    public async Task A_refused_login_yields_no_token_and_is_not_retried_at_once()
    {
        _nexus.On(HttpMethod.Post, "api/auth/login", HttpStatusCode.Unauthorized, """{"ok":false,"error":"E-poçt və ya şifrə yanlışdır."}""");
        var provider = Provider();

        (await provider.GetTokenAsync(CancellationToken.None)).Should().BeNull();
        (await provider.GetTokenAsync(CancellationToken.None)).Should().BeNull();

        _nexus.To(HttpMethod.Post, "api/auth/login").Should().ContainSingle("a failed login waits out its cooldown");
    }

    [Fact]
    public async Task Every_call_carries_the_bearer_token_and_a_401_triggers_one_fresh_login()
    {
        var first = Jwt(DateTimeOffset.UtcNow.AddHours(1), "first");
        var second = Jwt(DateTimeOffset.UtcNow.AddHours(1), "second");
        _nexus.On(HttpMethod.Post, "api/auth/login", HttpStatusCode.OK, LoginBody(first));
        var provider = Provider();
        await provider.GetTokenAsync(CancellationToken.None);

        // NexusERP restarted with a new key: the first token is rejected, the renewed one works.
        _nexus.On(HttpMethod.Get, "api/inventory/stocks", HttpStatusCode.Unauthorized, """{"ok":false,"error":"Giriş tələb olunur."}""")
            .On(HttpMethod.Post, "api/auth/login", HttpStatusCode.OK, LoginBody(second));
        var handler = new NexusErpAuthHandler(provider) { InnerHandler = new RenewedTokenGate(_nexus, second) };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://nexus.test/") };

        using var response = await client.GetAsync("api/inventory/stocks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _nexus.To(HttpMethod.Get, "api/inventory/stocks").Select(r => r.Authorization)
            .Should().Equal($"Bearer {first}");
        _nexus.To(HttpMethod.Post, "api/auth/login").Should().HaveCount(2);
    }

    [Fact]
    public async Task Without_a_token_the_call_is_answered_401_locally_and_never_sent()
    {
        _nexus.On(HttpMethod.Post, "api/auth/login", HttpStatusCode.ServiceUnavailable);
        var handler = new NexusErpAuthHandler(Provider()) { InnerHandler = _nexus };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://nexus.test/") };

        using var response = await client.GetAsync("api/inventory/stocks");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _nexus.To(HttpMethod.Get, "api/inventory/stocks").Should().BeEmpty();
    }

    /// <summary>Answers 200 to requests carrying <paramref name="acceptedToken"/>; everything else goes to the stub.</summary>
    private sealed class RenewedTokenGate(StubNexusErp stub, string acceptedToken) : DelegatingHandler(stub)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            request.Headers.Authorization?.Parameter == acceptedToken && request.RequestUri!.AbsolutePath != "/api/auth/login"
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"ok":true,"items":[]}""") })
                : base.SendAsync(request, cancellationToken);
    }
}
