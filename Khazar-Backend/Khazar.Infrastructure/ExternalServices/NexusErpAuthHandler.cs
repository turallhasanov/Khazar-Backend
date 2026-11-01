using System.Net;
using System.Net.Http.Headers;

namespace Khazar.Infrastructure.ExternalServices;

/// <summary>
/// Adds "Authorization: Bearer &lt;token&gt;" to every NexusERP request. On a 401 (token revoked early, or NexusERP
/// restarted with a new signing key) it logs in again once and retries; NexusERP answers 401 before running
/// the action, so retrying a POST cannot create a sale twice.
/// </summary>
public sealed class NexusErpAuthHandler(NexusErpTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetTokenAsync(cancellationToken);
        if (token is null)
            return Unauthorized(request);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();
        tokenProvider.Invalidate(token);

        var renewed = await tokenProvider.GetTokenAsync(cancellationToken);
        if (renewed is null)
            return Unauthorized(request);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", renewed);
        return await base.SendAsync(request, cancellationToken);
    }

    /// <summary>No token could be obtained: answer 401 locally instead of throwing, so callers see a plain failed call.</summary>
    private static HttpResponseMessage Unauthorized(HttpRequestMessage request) =>
        new(HttpStatusCode.Unauthorized) { RequestMessage = request };
}
