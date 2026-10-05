using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.UnitTests.Storage;
using NSubstitute;
using Refit;

namespace FixFlow.Mobile.UnitTests.Features.Auth;

public sealed class StartupViewModelTests : IAsyncDisposable
{
    private readonly InMemorySecureStore _secureStore = new();
    private readonly TestUserDatabase _userDatabase = new();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly HttpClient _httpClient = new() { BaseAddress = new Uri("https://api.test") };
    private readonly SessionStore _session;
    private readonly StartupViewModel _viewModel;

    public StartupViewModelTests()
    {
        _session = new SessionStore(_secureStore, TimeProvider.System);
        var api = RestService.ForGenerated<IFixFlowApi>(_httpClient);
        _viewModel = new StartupViewModel(new AuthService(api, api, _session, _userDatabase.Database, TimeProvider.System), _navigation);
    }

    [Fact]
    public async Task Should_Open_Work_Orders_Without_Network_When_Session_Is_Stored()
    {
        await _session.StartAsync(
            new AuthTokensResponse("access-1", DateTimeOffset.UtcNow.AddMinutes(15), "refresh-1", DateTimeOffset.UtcNow.AddDays(7)),
            new SessionUser(Guid.NewGuid(), "jan@fixflow.test", "Jan Kowalski", SessionUser.TechnicianRole));

        await _viewModel.StartAsync();

        await _navigation.Received(1).NavigateToRootAsync<WorkOrderListViewModel>();
        await _navigation.DidNotReceive().NavigateToRootAsync<LoginViewModel>();
        _userDatabase.Database.UserId.ShouldBe(_session.CurrentUser!.Id);
    }

    [Fact]
    public async Task Should_Open_Login_When_No_Session_Is_Stored()
    {
        await _viewModel.StartAsync();

        await _navigation.Received(1).NavigateToRootAsync<LoginViewModel>();
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        await _userDatabase.DisposeAsync();
    }
}
