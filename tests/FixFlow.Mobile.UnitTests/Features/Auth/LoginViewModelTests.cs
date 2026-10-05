using System.Net;
using System.Text;
using System.Text.Json;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Launching;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.UnitTests.Api;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Refit;

namespace FixFlow.Mobile.UnitTests.Features.Auth;

public sealed class LoginViewModelTests : IDisposable
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero));
    private readonly IConnectivityService _connectivity = Substitute.For<IConnectivityService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IUriLauncher _uriLauncher = Substitute.For<IUriLauncher>();
    private readonly StubHttpMessageHandler _handler;
    private readonly AuthenticationHandler _authenticationHandler;
    private readonly TokenRefresher _refresher;
    private readonly LoginViewModel _viewModel;
    private HttpStatusCode _readyStatus = HttpStatusCode.OK;
    private Func<HttpResponseMessage>? _loginResponse;
    private string _role = SessionUser.TechnicianRole;

    public LoginViewModelTests()
    {
        _connectivity.IsOnline.Returns(true);
        var session = new SessionStore(new InMemorySecureStore(), _timeProvider);
        _handler = new StubHttpMessageHandler(Route);
        var anonymousApi = RestService.ForGenerated<IFixFlowApi>(CreateClient(_handler));
        _refresher = new TokenRefresher(anonymousApi, session, _timeProvider);
        _authenticationHandler = new AuthenticationHandler(session, _refresher) { InnerHandler = _handler };
        var authService = new AuthService(anonymousApi, RestService.ForGenerated<IFixFlowApi>(CreateClient(_authenticationHandler)), session, _timeProvider);
        var readinessProbe = new ServerReadinessProbe(CreateClient(_handler), _timeProvider);
        var demoAccount = new DemoAccount("demo@fixflow.test", "demo-secret");
        _viewModel = new LoginViewModel(authService, readinessProbe, _connectivity, _navigation, _dialogs, _uriLauncher, demoAccount)
        {
            Email = " jan@fixflow.test ",
            Password = "secret",
        };
    }

    [Fact]
    public async Task Should_Navigate_To_Work_Orders_When_Technician_Logs_In()
    {
        await _viewModel.LoginCommand.ExecuteAsync(null);

        await _navigation.Received(1).NavigateToRootAsync<WorkOrderListViewModel>();
        _viewModel.ErrorMessage.ShouldBeNull();
        _viewModel.Password.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Show_Invalid_Credentials_When_Api_Returns_401()
    {
        _loginResponse = () => Problem(HttpStatusCode.Unauthorized, new { status = 401 });

        await _viewModel.LoginCommand.ExecuteAsync(null);

        _viewModel.ErrorMessage.ShouldBe(AppResources.LoginInvalidCredentials);
        await _navigation.DidNotReceive().NavigateToRootAsync<WorkOrderListViewModel>();
    }

    [Fact]
    public async Task Should_Show_Seconds_From_Retry_After_When_Login_Is_Rate_Limited()
    {
        _loginResponse = () =>
        {
            var response = Problem(HttpStatusCode.TooManyRequests, new { status = 429 });
            response.Headers.Add("Retry-After", "42");
            return response;
        };

        await _viewModel.LoginCommand.ExecuteAsync(null);

        _viewModel.ErrorMessage.ShouldNotBeNull().ShouldContain("42");
    }

    [Fact]
    public async Task Should_Show_Validation_Messages_From_Api_When_Request_Is_Invalid()
    {
        _loginResponse = () => Problem(HttpStatusCode.BadRequest, new { status = 400, errors = new { email = new List<string> { "Email must be a valid email address." } } });

        await _viewModel.LoginCommand.ExecuteAsync(null);

        _viewModel.ErrorMessage.ShouldBe("Email must be a valid email address.");
    }

    [Fact]
    public async Task Should_Show_Offline_Message_Without_Request_When_Device_Is_Offline()
    {
        _connectivity.IsOnline.Returns(false);

        await _viewModel.LoginCommand.ExecuteAsync(null);

        _viewModel.ErrorMessage.ShouldBe(AppResources.LoginOffline);
        _handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Offer_Web_Panel_When_User_Is_Not_Technician()
    {
        _role = "Dispatcher";
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await _viewModel.LoginCommand.ExecuteAsync(null);

        await _uriLauncher.Received(1).OpenAsync(LoginViewModel.WebPanelUri);
        await _navigation.DidNotReceive().NavigateToRootAsync<WorkOrderListViewModel>();
    }

    [Fact]
    public async Task Should_Show_Server_Unavailable_When_Server_Does_Not_Start_In_Time()
    {
        _readyStatus = HttpStatusCode.ServiceUnavailable;

        var login = _viewModel.LoginCommand.ExecuteAsync(null);
        while (!login.IsCompleted)
        {
            _timeProvider.Advance(TimeSpan.FromSeconds(3));
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        await login;
        _viewModel.ErrorMessage.ShouldBe(AppResources.LoginServerUnavailable);
        _viewModel.StatusMessage.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Log_In_With_Demo_Credentials_When_Demo_Login_Is_Used()
    {
        await _viewModel.DemoLoginCommand.ExecuteAsync(null);

        _viewModel.Email.ShouldBe("demo@fixflow.test");
        _viewModel.IsDemoLoginAvailable.ShouldBeTrue();
        await _navigation.Received(1).NavigateToRootAsync<WorkOrderListViewModel>();
    }

    [Theory]
    [InlineData("", "", false)]
    [InlineData("demo@fixflow.test", "", false)]
    [InlineData("demo@fixflow.test", "demo-secret", true)]
    public void Should_Offer_Demo_Login_Only_When_Build_Provides_Credentials(string email, string password, bool expected)
    {
        new DemoAccount(email, password).IsAvailable.ShouldBe(expected);
    }

    [Theory]
    [InlineData("", "secret", false)]
    [InlineData("jan@fixflow.test", "", false)]
    [InlineData("jan@fixflow.test", "secret", true)]
    public void Should_Enable_Login_Only_When_Email_And_Password_Are_Filled(string email, string password, bool expected)
    {
        _viewModel.Email = email;
        _viewModel.Password = password;

        _viewModel.LoginCommand.CanExecute(null).ShouldBe(expected);
    }

    public void Dispose()
    {
        _authenticationHandler.Dispose();
        _refresher.Dispose();
        _handler.Dispose();
    }

    private static HttpClient CreateClient(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };

    private HttpResponseMessage Route(HttpRequestMessage request) => request.RequestUri!.AbsolutePath switch
    {
        "/api/v1/system/ready" => new HttpResponseMessage(_readyStatus),
        "/api/v1/auth/login" => _loginResponse?.Invoke() ?? Json(new
        {
            accessToken = "access-1",
            accessTokenExpiresAt = _timeProvider.GetUtcNow().AddMinutes(15),
            refreshToken = "refresh-1",
            refreshTokenExpiresAt = _timeProvider.GetUtcNow().AddDays(7),
        }),
        "/api/v1/users/me" => Json(new { id = Guid.NewGuid(), email = "jan@fixflow.test", fullName = "Jan Kowalski", role = _role, isActive = true }),
        _ => new HttpResponseMessage(HttpStatusCode.NoContent),
    };

    private static HttpResponseMessage Json(object content) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Problem(HttpStatusCode statusCode, object content) =>
        new(statusCode) { Content = new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/problem+json") };
}
