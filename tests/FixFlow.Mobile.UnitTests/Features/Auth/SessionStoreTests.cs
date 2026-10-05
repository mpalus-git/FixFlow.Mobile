using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.Auth;
using Microsoft.Extensions.Time.Testing;

namespace FixFlow.Mobile.UnitTests.Features.Auth;

public sealed class SessionStoreTests
{
    private static readonly SessionUser Technician = new(Guid.NewGuid(), "jan@fixflow.test", "Jan Kowalski", SessionUser.TechnicianRole);

    private readonly InMemorySecureStore _secureStore = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero));
    private readonly SessionStore _store;

    public SessionStoreTests()
    {
        _store = new SessionStore(_secureStore, _timeProvider);
    }

    [Fact]
    public async Task Should_Restore_User_Without_Access_Token_When_Session_Was_Saved()
    {
        await _store.StartAsync(Tokens("access-1", "refresh-1"), Technician);
        var restored = new SessionStore(_secureStore, _timeProvider);

        var user = await restored.RestoreAsync();

        user.ShouldBe(Technician);
        restored.AccessToken.ShouldBeNull();
        restored.HasValidAccessToken.ShouldBeFalse();
        (await restored.GetRefreshTokenAsync()).ShouldBe("refresh-1");
    }

    [Fact]
    public async Task Should_Keep_Access_Token_Only_In_Memory_When_Session_Starts()
    {
        await _store.StartAsync(Tokens("access-1", "refresh-1"), Technician);

        _store.AccessToken.ShouldBe("access-1");
        _secureStore.Values[SessionStore.SessionKey].ShouldNotContain("access-1");
    }

    [Fact]
    public async Task Should_Replace_Refresh_Token_And_Keep_User_When_Tokens_Are_Rotated()
    {
        await _store.StartAsync(Tokens("access-1", "refresh-1"), Technician);

        await _store.UpdateTokensAsync(Tokens("access-2", "refresh-2"));

        _store.AccessToken.ShouldBe("access-2");
        (await _store.GetRefreshTokenAsync()).ShouldBe("refresh-2");
        (await new SessionStore(_secureStore, _timeProvider).RestoreAsync()).ShouldBe(Technician);
    }

    [Fact]
    public async Task Should_Treat_Access_Token_As_Expired_When_It_Expires_Within_Skew()
    {
        await _store.StartAsync(Tokens("access-1", "refresh-1", expiresIn: TimeSpan.FromMinutes(1)), Technician);
        _store.HasValidAccessToken.ShouldBeTrue();

        _timeProvider.Advance(TimeSpan.FromMinutes(1) - SessionStore.ExpirySkew);

        _store.HasValidAccessToken.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Remove_Stored_Session_And_Notify_When_Session_Ends()
    {
        await _store.StartAsync(Tokens("access-1", "refresh-1"), Technician);
        SessionEndReason? endReason = null;
        _store.SessionEnded += (_, args) => endReason = args.Reason;

        _store.End(SessionEndReason.Expired);

        endReason.ShouldBe(SessionEndReason.Expired);
        _store.CurrentUser.ShouldBeNull();
        _store.AccessToken.ShouldBeNull();
        _secureStore.Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Not_Persist_Anything_When_Pending_Tokens_Are_Used()
    {
        _store.UsePendingTokens(Tokens("access-1", "refresh-1"));

        _store.AccessToken.ShouldBe("access-1");
        _secureStore.Values.ShouldBeEmpty();
        (await _store.GetRefreshTokenAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Return_No_User_When_Stored_Session_Is_Corrupted()
    {
        _secureStore.Values[SessionStore.SessionKey] = "{broken";

        (await _store.RestoreAsync()).ShouldBeNull();
    }

    private AuthTokensResponse Tokens(string accessToken, string refreshToken, TimeSpan? expiresIn = null) =>
        new(
            accessToken,
            _timeProvider.GetUtcNow() + (expiresIn ?? TimeSpan.FromMinutes(15)),
            refreshToken,
            _timeProvider.GetUtcNow().AddDays(7));
}
