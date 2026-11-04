using System.Net;
using System.Text;

namespace Khazar.WebAPI.Tests.NexusErp;

/// <summary>
/// NexusERP as an HttpMessageHandler: answers configured routes with canned JSON (the shapes NexusERP sends, which its
/// own integration tests pin) and records every request, so a test sees exactly what Khazar sent.
/// </summary>
public sealed class StubNexusErp : HttpMessageHandler
{
    private readonly List<(HttpMethod Method, string Path, Func<HttpResponseMessage> Respond)> _routes = [];

    public List<RecordedRequest> Requests { get; } = [];

    public sealed record RecordedRequest(HttpMethod Method, string Path, string? Body, string? Authorization);

    /// <summary>Answers <paramref name="method"/> <paramref name="path"/> (relative, no leading slash). Later setups win.</summary>
    public StubNexusErp On(HttpMethod method, string path, HttpStatusCode status, string? json = null)
    {
        _routes.Add((method, path, () => new HttpResponseMessage(status)
        {
            Content = json is null ? new ByteArrayContent([]) : new StringContent(json, Encoding.UTF8, "application/json")
        }));
        return this;
    }

    public StubNexusErp Throw(HttpMethod method, string path, Exception exception)
    {
        _routes.Add((method, path, () => throw exception));
        return this;
    }

    public IEnumerable<RecordedRequest> To(HttpMethod method, string path) =>
        Requests.Where(r => r.Method == method && r.Path == path);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = request.RequestUri!.PathAndQuery.TrimStart('/');
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(request.Method, path, body, request.Headers.Authorization?.ToString()));

        var route = _routes.LastOrDefault(r => r.Method == request.Method && r.Path == path);
        var response = route.Respond is null ? new HttpResponseMessage(HttpStatusCode.NotFound) : route.Respond();
        response.RequestMessage = request;
        return response;
    }
}
