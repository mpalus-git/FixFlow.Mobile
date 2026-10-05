using System.Net;
using System.Net.Http.Headers;

namespace FixFlow.Mobile.Core.Features.Auth;

public sealed class AuthenticationHandler(SessionStore session, TokenRefresher refresher) : DelegatingHandler
{
    private const string BearerScheme = "Bearer";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!session.HasValidAccessToken)
        {
            await refresher.RefreshAsync(session.AccessToken, cancellationToken);
        }

        var accessToken = Authorize(request);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        if (await refresher.RefreshAsync(accessToken, cancellationToken) != TokenRefreshResult.Refreshed)
        {
            return response;
        }

        response.Dispose();
        Authorize(request);
        return await base.SendAsync(request, cancellationToken);
    }

    private string? Authorize(HttpRequestMessage request)
    {
        var accessToken = session.AccessToken;
        request.Headers.Authorization = accessToken is null ? null : new AuthenticationHeaderValue(BearerScheme, accessToken);
        return accessToken;
    }
}
