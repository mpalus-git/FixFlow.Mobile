using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Api.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.Mobile.Core.Features.Auth;

public sealed class AuthService(
    [FromKeyedServices(ApiServiceCollectionExtensions.AnonymousClientKey)] IFixFlowApi anonymousApi,
    IFixFlowApi api,
    SessionStore session,
    TimeProvider timeProvider)
{
    public static readonly TimeSpan RevokeTimeout = TimeSpan.FromSeconds(5);

    public Task<SessionUser?> RestoreSessionAsync() => session.RestoreAsync();

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        using var loginResponse = await anonymousApi.LoginAsync(new LoginRequest(email, password), cancellationToken);
        if (loginResponse is not { IsSuccessful: true, Content: { } tokens })
        {
            return LoginResult.Failed(ApiErrorMapper.FromResponse(loginResponse));
        }

        session.UsePendingTokens(tokens);
        using var userResponse = await api.GetCurrentUserAsync(cancellationToken);
        if (userResponse is not { IsSuccessful: true, Content: { } profile })
        {
            await RevokeAsync(tokens.RefreshToken);
            session.DiscardPendingTokens();
            return LoginResult.Failed(ApiErrorMapper.FromResponse(userResponse));
        }

        var user = new SessionUser(profile.Id, profile.Email, profile.FullName, profile.Role);
        if (!user.IsTechnician)
        {
            await RevokeAsync(tokens.RefreshToken);
            session.DiscardPendingTokens();
            return LoginResult.NotTechnician();
        }

        await session.StartAsync(tokens, user);
        return LoginResult.Succeeded(user);
    }

    public async Task LogoutAsync()
    {
        if (await session.GetRefreshTokenAsync() is { } refreshToken)
        {
            await RevokeAsync(refreshToken);
        }

        session.End(SessionEndReason.SignedOut);
    }

    private async Task RevokeAsync(string refreshToken)
    {
        using var timeout = new CancellationTokenSource(RevokeTimeout, timeProvider);
        try
        {
            using var response = await api.LogoutAsync(new LogoutRequest(refreshToken), timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
        }
    }
}
