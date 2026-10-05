using System.Text.Json;
using FixFlow.Mobile.Core.Api;

namespace FixFlow.Mobile.Core.Features.Auth;

public sealed class SessionStore(ISecureStore secureStore, TimeProvider timeProvider)
{
    public const string SessionKey = "session";

    public static readonly TimeSpan ExpirySkew = TimeSpan.FromSeconds(30);

    private volatile AccessTokenState? _accessToken;

    public event EventHandler<SessionEndedEventArgs>? SessionEnded;

    public SessionUser? CurrentUser { get; private set; }

    public string? AccessToken => _accessToken?.Value;

    public bool HasValidAccessToken =>
        _accessToken is { } token && token.ExpiresAt - ExpirySkew > timeProvider.GetUtcNow();

    public async Task<SessionUser?> RestoreAsync()
    {
        CurrentUser = (await ReadAsync())?.User;
        return CurrentUser;
    }

    public void UsePendingTokens(AuthTokensResponse tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        _accessToken = new AccessTokenState(tokens.AccessToken, tokens.AccessTokenExpiresAt);
    }

    public void DiscardPendingTokens() => _accessToken = null;

    public async Task StartAsync(AuthTokensResponse tokens, SessionUser user)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        await WriteAsync(new StoredSession(tokens.RefreshToken, user));
        CurrentUser = user;
        UsePendingTokens(tokens);
    }

    public async Task UpdateTokensAsync(AuthTokensResponse tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var stored = await ReadAsync() ?? throw new InvalidOperationException("There is no stored session to update.");
        await WriteAsync(stored with { RefreshToken = tokens.RefreshToken });
        UsePendingTokens(tokens);
    }

    public async Task<string?> GetRefreshTokenAsync() => (await ReadAsync())?.RefreshToken;

    public void End(SessionEndReason reason)
    {
        _accessToken = null;
        CurrentUser = null;
        secureStore.Remove(SessionKey);
        SessionEnded?.Invoke(this, new SessionEndedEventArgs(reason));
    }

    private async Task<StoredSession?> ReadAsync()
    {
        var json = await secureStore.GetAsync(SessionKey);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StoredSession>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Task WriteAsync(StoredSession session) =>
        secureStore.SetAsync(SessionKey, JsonSerializer.Serialize(session));

    private sealed record AccessTokenState(string Value, DateTimeOffset ExpiresAt);

    private sealed record StoredSession(string RefreshToken, SessionUser User);
}
