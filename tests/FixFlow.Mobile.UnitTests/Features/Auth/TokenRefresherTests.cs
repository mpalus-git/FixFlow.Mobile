using System.Net;
using System.Text;
using System.Text.Json;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.UnitTests.Api;
using Microsoft.Extensions.Time.Testing;
using Refit;

namespace FixFlow.Mobile.UnitTests.Features.Auth;

public sealed class TokenRefresherTests : IDisposable
{
    private static readonly SessionUser Technician = new(Guid.NewGuid(), "jan@fixflow.test", "Jan Kowalski", SessionUser.TechnicianRole);

    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero));
    private readonly InMemorySecureStore _secureStore = new();
    private readonly SessionStore _session;
    private readonly List<string> _sentRefreshTokens = [];
    private Func<int, Task<HttpResponseMessage>> _respond;
    private TokenRefresher? _refresher;
    private StubHttpMessageHandler? _handler;

    public TokenRefresherTests()
    {
        _session = new SessionStore(_secureStore, _timeProvider);
        _respond = attempt => Task.FromResult(TokensResponse(attempt + 1));
    }

    [Fact]
    public async Task Should_Send_Single_Refresh_When_Ten_Requests_Refresh_In_Parallel()
    {
        await StartSessionAsync();
        var release = new TaskCompletionSource();
        _respond = async attempt =>
        {
            await release.Task;
            return TokensResponse(attempt + 2);
        };
        var refresher = CreateRefresher();

        var refreshes = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => refresher.RefreshAsync("access-1", TestContext.Current.CancellationToken)))
            .ToList();
        await Task.Delay(50, TestContext.Current.CancellationToken);
        release.SetResult();
        var results = await Task.WhenAll(refreshes);

        results.ShouldAllBe(result => result == TokenRefreshResult.Refreshed);
        _sentRefreshTokens.ShouldBe(["refresh-1"]);
        _session.AccessToken.ShouldBe("access-2");
        (await _session.GetRefreshTokenAsync()).ShouldBe("refresh-2");
    }

    [Fact]
    public async Task Should_Send_Rotated_Refresh_Token_When_Token_Is_Refreshed_Again()
    {
        await StartSessionAsync();
        _respond = attempt => Task.FromResult(TokensResponse(attempt + 2));
        var refresher = CreateRefresher();

        await refresher.RefreshAsync("access-1", TestContext.Current.CancellationToken);
        await refresher.RefreshAsync("access-2", TestContext.Current.CancellationToken);

        _sentRefreshTokens.ShouldBe(["refresh-1", "refresh-2"]);
    }

    [Fact]
    public async Task Should_End_Session_When_Refresh_Token_Is_Rejected()
    {
        await StartSessionAsync();
        _respond = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        SessionEndReason? endReason = null;
        _session.SessionEnded += (_, args) => endReason = args.Reason;

        var result = await CreateRefresher().RefreshAsync("access-1", TestContext.Current.CancellationToken);

        result.ShouldBe(TokenRefreshResult.Expired);
        endReason.ShouldBe(SessionEndReason.Expired);
        _secureStore.Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Keep_Session_When_Refresh_Fails_With_Network_Error()
    {
        await StartSessionAsync();
        _respond = _ => throw new HttpRequestException("Network is unreachable");

        var result = await CreateRefresher().RefreshAsync("access-1", TestContext.Current.CancellationToken);

        result.ShouldBe(TokenRefreshResult.Unavailable);
        _session.CurrentUser.ShouldBe(Technician);
        (await _session.GetRefreshTokenAsync()).ShouldBe("refresh-1");
    }

    [Fact]
    public async Task Should_Return_Expired_Without_Request_When_No_Session_Is_Stored()
    {
        var result = await CreateRefresher().RefreshAsync(null, TestContext.Current.CancellationToken);

        result.ShouldBe(TokenRefreshResult.Expired);
        _sentRefreshTokens.ShouldBeEmpty();
    }

    public void Dispose()
    {
        _refresher?.Dispose();
        _handler?.Dispose();
    }

    private Task StartSessionAsync() =>
        _session.StartAsync(
            new AuthTokensResponse("access-1", _timeProvider.GetUtcNow().AddMinutes(15), "refresh-1", _timeProvider.GetUtcNow().AddDays(7)),
            Technician);

    private TokenRefresher CreateRefresher()
    {
        _handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            int attempt;
            lock (_sentRefreshTokens)
            {
                attempt = _sentRefreshTokens.Count;
                _sentRefreshTokens.Add(body.RootElement.GetProperty("refreshToken").GetString()!);
            }

            return await _respond(attempt);
        });
        var api = RestService.ForGenerated<IFixFlowApi>(new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") });
        _refresher = new TokenRefresher(api, _session);
        return _refresher;
    }

    private HttpResponseMessage TokensResponse(int version)
    {
        var accessExpiresAt = _timeProvider.GetUtcNow().AddMinutes(15).ToString("O");
        var refreshExpiresAt = _timeProvider.GetUtcNow().AddDays(7).ToString("O");
        var json = $$"""{"accessToken":"access-{{version}}","accessTokenExpiresAt":"{{accessExpiresAt}}","refreshToken":"refresh-{{version}}","refreshTokenExpiresAt":"{{refreshExpiresAt}}"}""";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
