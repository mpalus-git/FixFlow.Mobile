using System.Net;
using System.Text;
using System.Text.Json;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.Core.Screens;
using FixFlow.Mobile.UnitTests.Api;
using FixFlow.Mobile.UnitTests.Features.Auth;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Refit;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class WorkOrderListViewModelTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly InMemorySecureStore _secureStore = new();
    private readonly TestUserDatabase _userDatabase = new();
    private readonly IConnectivityService _connectivity = Substitute.For<IConnectivityService>();
    private readonly StubHttpMessageHandler _handler;
    private readonly SessionStore _session;
    private readonly WorkOrderRepository _workOrders;
    private readonly WorkOrderListViewModel _viewModel;
    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;
    private int _remoteCount = 2;

    public WorkOrderListViewModelTests()
    {
        _connectivity.IsOnline.Returns(true);
        _respond = (request, _) => Task.FromResult(Route(request));
        _session = new SessionStore(_secureStore, _timeProvider);
        _handler = new StubHttpMessageHandler((request, cancellationToken) => _respond(request, cancellationToken));
        var httpClient = new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };
        var api = RestService.ForGenerated<IFixFlowApi>(httpClient);
        var clock = BusinessClock.CreateForWarsaw(_timeProvider);
        var formatter = new DisplayFormatter(new LanguageService(Substitute.For<ISettingsStore>()), clock);
        _workOrders = new WorkOrderRepository(_userDatabase.Database);
        var sync = new WorkOrderSyncService(
            api,
            new ServerReadinessProbe(httpClient, _timeProvider),
            new ServerWarmup(),
            _workOrders,
            new SyncStateRepository(_userDatabase.Database, _timeProvider));
        _viewModel = new WorkOrderListViewModel(
            new ScreenState(formatter),
            new AuthService(api, api, _session, _userDatabase.Database, _timeProvider),
            _workOrders,
            sync,
            new WorkOrderListPresenter(new WorkOrderSectionClassifier(clock), formatter),
            _connectivity);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Show_Local_Work_Orders_Before_Refresh_Completes()
    {
        await _workOrders.ReplaceAllAsync([TestWorkOrders.Create("ZL-LOCAL", Now.AddDays(1))]);
        var release = new TaskCompletionSource();
        _respond = async (request, _) =>
        {
            await release.Task;
            return Route(request);
        };

        var load = _viewModel.LoadCommand.ExecuteAsync(null);
        while (_handler.Requests.Count == 0)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        _viewModel.Screen.Status.ShouldBe(ScreenStatus.Content);
        _viewModel.Groups.SelectMany(group => group).Select(item => item.Number).ShouldBe(["ZL-LOCAL"]);
        release.SetResult();
        await load;
        _viewModel.Groups.SelectMany(group => group).Count().ShouldBe(2);
    }

    [Fact]
    public async Task Should_Show_Empty_State_When_Technician_Has_No_Work_Orders()
    {
        _remoteCount = 0;

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.Screen.Status.ShouldBe(ScreenStatus.Empty);
    }

    [Fact]
    public async Task Should_Show_Error_With_Retry_When_Refresh_Fails_Without_Local_Data()
    {
        _respond = (request, _) => IsListRequest(request) ? throw new HttpRequestException("Network is unreachable") : Task.FromResult(Route(request));

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.Screen.Status.ShouldBe(ScreenStatus.Error);
        _viewModel.Screen.ErrorMessage.ShouldBe(AppResources.WorkOrderListLoadFailed);
    }

    [Fact]
    public async Task Should_End_Session_When_Technician_Logs_Out()
    {
        await _session.StartAsync(
            new AuthTokensResponse("access-1", Now.AddMinutes(15), "refresh-1", Now.AddDays(7)),
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

    private static bool IsListRequest(HttpRequestMessage request) => request.RequestUri!.AbsolutePath == "/api/v1/work-orders";

    private HttpResponseMessage Route(HttpRequestMessage request)
    {
        if (!IsListRequest(request))
        {
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        var items = Enumerable.Range(1, _remoteCount).Select(index => new
        {
            id = Guid.NewGuid(),
            number = $"ZL-2026-{index:D4}",
            deviceId = Guid.NewGuid(),
            deviceSerialNumber = "SN-1",
            deviceModel = "Daikin",
            clientId = Guid.NewGuid(),
            clientName = "Piekarnia",
            description = "Przegląd",
            priority = "Normal",
            status = "Assigned",
            dueDate = Now.AddDays(index),
            isOverdue = false,
            createdAt = Now.AddDays(-1),
        });
        var json = JsonSerializer.Serialize(new { items, page = 1, pageSize = 100, totalCount = _remoteCount });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
