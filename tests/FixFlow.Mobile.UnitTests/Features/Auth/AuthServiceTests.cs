using System.Net;
using System.Text;
using System.Text.Json;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.UnitTests.Api;
using Microsoft.Extensions.Time.Testing;
using Refit;

namespace FixFlow.Mobile.UnitTests.Features.Auth;

public sealed class AuthServiceTests : IDisposable
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero));
    private readonly InMemorySecureStore _secureStore = new();
    private readonly SessionStore _session;
    private readonly StubHttpMessageHandler _handler;
    private readonly AuthenticationHandler _authenticationHandler;
    private readonly TokenRefresher _refresher;
    private readonly AuthService _service;
    private readonly List<string> _revokedTokens = [];
    private Func<HttpResponseMessage> _loginResponse;
    private string _role = SessionUser.TechnicianRole;

    public AuthServiceTests()
    {
        _session = new SessionStore(_secureStore, _timeProvider);
        _loginResponse = () => Json(
            HttpStatusCode.OK,
            new { accessToken = "access-1", accessTokenExpiresAt = _timeProvider.GetUtcNow().AddMinutes(15), refreshToken = "refresh-1", refreshTokenExpiresAt = _timeProvider.GetUtcNow().AddDays(7) });
        _handler = new StubHttpMessageHandler(RouteAsync);
        var anonymousApi = RestService.ForGenerated<IFixFlowApi>(new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") });
        _refresher = new TokenRefresher(anonymousApi, _session, _timeProvider);
        _authenticationHandler = new AuthenticationHandler(_session, _refresher) { InnerHandler = _handler };
        var api = RestService.ForGenerated<IFixFlowApi>(new HttpClient(_authenticationHandler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") });
        _service = new AuthService(anonymousApi, api, _session, _timeProvider);
    }

    [Fact]
    public async Task Should_Store_Session_When_Technician_Logs_In()
    {
        var result = await _service.LoginAsync("jan@fixflow.test", "secret", TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(LoginOutcome.Succeeded);
        result.User.ShouldBe(new SessionUser(UserId, "jan@fixflow.test", "Jan Kowalski", SessionUser.TechnicianRole));
        _session.AccessToken.ShouldBe("access-1");
        (await _session.GetRefreshTokenAsync()).ShouldBe("refresh-1");
        _handler.Requests.Single(request => request.RequestUri!.AbsolutePath == "/api/v1/users/me")
            .Headers.Authorization!.Parameter.ShouldBe("access-1");
    }

    [Fact]
    public async Task Should_Fail_Without_Profile_Request_When_Credentials_Are_Invalid()
    {
        _loginResponse = () => Json(HttpStatusCode.Unauthorized, new { status = 401, detail = "Invalid email or password." });

        var result = await _service.LoginAsync("jan@fixflow.test", "wrong", TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(LoginOutcome.Failed);
        result.Error!.Kind.ShouldBe(ApiErrorKind.Unauthorized);
        _handler.Requests.ShouldHaveSingleItem();
        _secureStore.Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Return_Retry_After_When_Login_Is_Rate_Limited()
    {
        _loginResponse = () =>
        {
            var response = Json(HttpStatusCode.TooManyRequests, new { status = 429, detail = "Too many authentication requests. Try again later." });
            response.Headers.Add("Retry-After", "42");
            return response;
        };

        var result = await _service.LoginAsync("jan@fixflow.test", "secret", TestContext.Current.CancellationToken);

        result.Error!.RetryAfter.ShouldBe(TimeSpan.FromSeconds(42));
    }

    [Fact]
    public async Task Should_Revoke_And_Not_Store_Session_When_User_Is_Not_Technician()
    {
        _role = "Dispatcher";

        var result = await _service.LoginAsync("anna@fixflow.test", "secret", TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(LoginOutcome.NotTechnician);
        _revokedTokens.ShouldBe(["refresh-1"]);
        _session.AccessToken.ShouldBeNull();
        _secureStore.Values.ShouldBeEmpty();
    }

    public void Dispose()
    {
        _authenticationHandler.Dispose();
        _refresher.Dispose();
        _handler.Dispose();
    }

    private async Task<HttpResponseMessage> RouteAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        switch (request.RequestUri!.AbsolutePath)
        {
            case "/api/v1/auth/login":
                return _loginResponse();
            case "/api/v1/users/me":
                return Json(HttpStatusCode.OK, new { id = UserId, email = "jan@fixflow.test", fullName = "Jan Kowalski", role = _role, isActive = true });
            case "/api/v1/auth/logout":
                using (var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)))
                {
                    _revokedTokens.Add(body.RootElement.GetProperty("refreshToken").GetString()!);
                }

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            default:
                return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, object content) =>
        new(statusCode)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(content),
                Encoding.UTF8,
                statusCode == HttpStatusCode.OK ? "application/json" : "application/problem+json"),
        };
}
