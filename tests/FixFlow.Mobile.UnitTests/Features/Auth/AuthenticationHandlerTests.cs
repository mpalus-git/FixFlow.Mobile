using System.Net;
using System.Text;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.UnitTests.Api;
using Microsoft.Extensions.Time.Testing;
using Refit;

namespace FixFlow.Mobile.UnitTests.Features.Auth;

public sealed class AuthenticationHandlerTests : IDisposable
{
    private static readonly SessionUser Technician = new(Guid.NewGuid(), "jan@fixflow.test", "Jan Kowalski", SessionUser.TechnicianRole);

    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero));
    private readonly InMemorySecureStore _secureStore = new();
    private readonly SessionStore _session;
    private readonly StubHttpMessageHandler _refreshHandler;
    private readonly TokenRefresher _refresher;
    private readonly List<string?> _sentAccessTokens = [];
    private HttpStatusCode _refreshStatus = HttpStatusCode.OK;
    private Func<string?, HttpStatusCode> _apiStatus = _ => HttpStatusCode.OK;

    public AuthenticationHandlerTests()
    {
        _session = new SessionStore(_secureStore, _timeProvider);
        _refreshHandler = new StubHttpMessageHandler(_ => _refreshStatus == HttpStatusCode.OK
            ? TokensResponse("access-2", "refresh-2")
            : new HttpResponseMessage(_refreshStatus));
        var anonymousApi = RestService.ForGenerated<IFixFlowApi>(
            new HttpClient(_refreshHandler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") });
        _refresher = new TokenRefresher(anonymousApi, _session, _timeProvider);
    }

    [Fact]
    public async Task Should_Send_Bearer_Token_When_Access_Token_Is_Valid()
    {
        await StartSessionAsync();

        using var response = await SendAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _sentAccessTokens.ShouldBe(["access-1"]);
        _refreshHandler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Refresh_Before_Request_When_Session_Was_Restored_Without_Access_Token()
    {
        await StartSessionAsync();
        await RestoreInNewStoreAsync();

        using var response = await SendAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _sentAccessTokens.ShouldBe(["access-2"]);
    }

    [Fact]
    public async Task Should_Retry_Once_With_New_Token_When_Api_Returns_401()
    {
        await StartSessionAsync();
        _apiStatus = token => token == "access-1" ? HttpStatusCode.Unauthorized : HttpStatusCode.OK;

        using var response = await SendAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _sentAccessTokens.ShouldBe(["access-1", "access-2"]);
        _refreshHandler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Not_Retry_Again_When_New_Token_Is_Also_Rejected()
    {
        await StartSessionAsync();
        _apiStatus = _ => HttpStatusCode.Unauthorized;

        using var response = await SendAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        _sentAccessTokens.ShouldBe(["access-1", "access-2"]);
    }

    [Fact]
    public async Task Should_Return_401_And_End_Session_When_Refresh_Is_Rejected()
    {
        await StartSessionAsync();
        _apiStatus = _ => HttpStatusCode.Unauthorized;
        _refreshStatus = HttpStatusCode.Unauthorized;

        using var response = await SendAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        _sentAccessTokens.ShouldBe(["access-1"]);
        _session.CurrentUser.ShouldBeNull();
    }

    public void Dispose()
    {
        _refresher.Dispose();
        _refreshHandler.Dispose();
    }

    private Task StartSessionAsync() =>
        _session.StartAsync(
            new AuthTokensResponse("access-1", _timeProvider.GetUtcNow().AddMinutes(15), "refresh-1", _timeProvider.GetUtcNow().AddDays(7)),
            Technician);

    private async Task RestoreInNewStoreAsync()
    {
        _session.DiscardPendingTokens();
        await _session.RestoreAsync();
    }

    private async Task<HttpResponseMessage> SendAsync()
    {
        using var apiHandler = new StubHttpMessageHandler(request =>
        {
            var token = request.Headers.Authorization?.Parameter;
            _sentAccessTokens.Add(token);
            return new HttpResponseMessage(_apiStatus(token));
        });
        using var handler = new AuthenticationHandler(_session, _refresher) { InnerHandler = apiHandler };
        using var client = new HttpClient(handler, disposeHandler: false);

        return await client.GetAsync(new Uri("https://api.test/api/v1/users/me"), TestContext.Current.CancellationToken);
    }

    private HttpResponseMessage TokensResponse(string accessToken, string refreshToken)
    {
        var accessExpiresAt = _timeProvider.GetUtcNow().AddMinutes(15).ToString("O");
        var refreshExpiresAt = _timeProvider.GetUtcNow().AddDays(7).ToString("O");
        var json = $$"""{"accessToken":"{{accessToken}}","accessTokenExpiresAt":"{{accessExpiresAt}}","refreshToken":"{{refreshToken}}","refreshTokenExpiresAt":"{{refreshExpiresAt}}"}""";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
