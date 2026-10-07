using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.Mobile.Core.Features.Auth;

public sealed class AuthService(
    [FromKeyedServices(ApiServiceCollectionExtensions.AnonymousClientKey)] IFixFlowApi anonymousApi,
    IFixFlowApi api,
    SessionStore session,
    UserDatabase userDatabase,
    PhotoStore photos,
    OrphanedPhotoCleaner photoCleaner,
    TimeProvider timeProvider)
{
    public static readonly TimeSpan RevokeTimeout = TimeSpan.FromSeconds(5);

    public async Task<SessionUser?> RestoreSessionAsync()
    {
        var user = await session.RestoreAsync();
        if (user is not null)
        {
            await userDatabase.OpenAsync(user.Id);
            await photoCleaner.CleanAsync();
        }

        return user;
    }

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
        await userDatabase.OpenAsync(user.Id);
        await photoCleaner.CleanAsync();
        return LoginResult.Succeeded(user);
    }

    public async Task LogoutAsync()
    {
        if (await session.GetRefreshTokenAsync() is { } refreshToken)
        {
            await RevokeAsync(refreshToken);
        }

        try
        {
            if (userDatabase.UserId is not null)
            {
                photos.DeleteAll();
            }

            await userDatabase.DeleteAsync();
        }
        finally
        {
            session.End(SessionEndReason.SignedOut);
        }
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
