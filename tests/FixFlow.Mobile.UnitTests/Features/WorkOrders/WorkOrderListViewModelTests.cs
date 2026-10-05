using System.Net;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Screens;
using FixFlow.Mobile.UnitTests.Api;
using FixFlow.Mobile.UnitTests.Features.Auth;
using Microsoft.Extensions.Time.Testing;
using FixFlow.Mobile.UnitTests.Storage;
using NSubstitute;
using Refit;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class WorkOrderListViewModelTests : IAsyncDisposable
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly InMemorySecureStore _secureStore = new();
    private readonly TestUserDatabase _userDatabase = new();
    private readonly StubHttpMessageHandler _handler = new(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
    private readonly SessionStore _session;
    private readonly WorkOrderListViewModel _viewModel;

    public WorkOrderListViewModelTests()
    {
        _session = new SessionStore(_secureStore, _timeProvider);
        var formatter = new DisplayFormatter(
            new LanguageService(Substitute.For<ISettingsStore>()),
            BusinessClock.CreateForWarsaw(_timeProvider));
        var api = RestService.ForGenerated<IFixFlowApi>(new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") });
        _viewModel = new WorkOrderListViewModel(new ScreenState(formatter), new AuthService(api, api, _session, _userDatabase.Database, _timeProvider));
    }

    [Fact]
    public async Task Should_Show_Empty_State_When_Refresh_Finds_No_Work_Orders()
    {
        await _viewModel.RefreshCommand.ExecuteAsync(null);

        _viewModel.Screen.Status.ShouldBe(ScreenStatus.Empty);
    }

    [Fact]
    public async Task Should_End_Session_When_Technician_Logs_Out()
    {
        await _session.StartAsync(
            new AuthTokensResponse("access-1", _timeProvider.GetUtcNow().AddMinutes(15), "refresh-1", _timeProvider.GetUtcNow().AddDays(7)),
            new SessionUser(Guid.NewGuid(), "jan@fixflow.test", "Jan Kowalski", SessionUser.TechnicianRole));

        await _viewModel.LogoutCommand.ExecuteAsync(null);

        _secureStore.Values.ShouldBeEmpty();
        _handler.Requests.ShouldHaveSingleItem().RequestUri!.AbsolutePath.ShouldBe("/api/v1/auth/logout");
    }

    public async ValueTask DisposeAsync()
    {
        _handler.Dispose();
        await _userDatabase.DisposeAsync();
    }
}
