using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Api.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.Mobile.Core.Features.Auth;

public sealed class TokenRefresher(
    [FromKeyedServices(ApiServiceCollectionExtensions.AnonymousClientKey)] IFixFlowApi api,
    SessionStore session) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<TokenRefreshResult> RefreshAsync(string? rejectedAccessToken, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (session.HasValidAccessToken && session.AccessToken != rejectedAccessToken)
            {
                return TokenRefreshResult.Refreshed;
            }

            var refreshToken = await session.GetRefreshTokenAsync();
            if (refreshToken is null)
            {
                return EndSession();
            }

            using var response = await api.RefreshTokensAsync(new RefreshRequest(refreshToken), cancellationToken);
            if (response is { IsSuccessful: true, Content: { } tokens })
            {
                await session.UpdateTokensAsync(tokens);
                return TokenRefreshResult.Refreshed;
            }

            var error = ApiErrorMapper.FromResponse(response);
            if (error.Kind is ApiErrorKind.Unauthorized or ApiErrorKind.Validation)
            {
                return EndSession();
            }

            return TokenRefreshResult.Unavailable;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private TokenRefreshResult EndSession()
    {
        if (session.CurrentUser is not null)
        {
            session.End(SessionEndReason.Expired);
        }

        return TokenRefreshResult.Expired;
    }
}
