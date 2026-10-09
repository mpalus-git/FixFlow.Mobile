using System.Net;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Features.Parts;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.Core.Screens;
using FixFlow.Mobile.UnitTests.Api;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Refit;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class WorkOrderListViewModelTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly TestUserDatabase _userDatabase = new();
    private readonly IConnectivityService _connectivity = Substitute.For<IConnectivityService>();
    private readonly ImmediateUiDispatcher _ui = new();
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly StubHttpMessageHandler _handler;
    private readonly WorkOrderRepository _workOrders;
    private readonly OutboxRepository _outbox;
    private readonly WorkOrderListViewModel _viewModel;
    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;
    private int _remoteCount = 2;

    public WorkOrderListViewModelTests()
    {
        _connectivity.IsOnline.Returns(true);
        _respond = (request, _) => Task.FromResult(Route(request));
        _handler = new StubHttpMessageHandler((request, cancellationToken) => _respond(request, cancellationToken));
        var httpClient = new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };
        var api = RestService.ForGenerated<IFixFlowApi>(httpClient);
        var clock = BusinessClock.CreateForWarsaw(_timeProvider);
        var formatter = new DisplayFormatter(new LanguageService(Substitute.For<ISettingsStore>()), clock);
        _workOrders = new WorkOrderRepository(_userDatabase.Database);
        var warmup = new ServerWarmup();
        var syncStates = new SyncStateRepository(_userDatabase.Database, _timeProvider);
        var sync = new WorkOrderSyncService(api, _workOrders, syncStates);
        _outbox = new OutboxRepository(_userDatabase.Database);
        var coordinator = new SyncCoordinator(
            _userDatabase.Database,
            _connectivity,
            warmup,
            () => new ServerReadinessProbe(httpClient, _timeProvider),
            () => new OutboxDispatcher(api, _outbox, new ServiceEntryRepository(_userDatabase.Database), new PhotoUploadClient(httpClient), new PhotoStore(_userDatabase.Options, _userDatabase.Database), new OutboxRetryPolicy(() => 0.5), _timeProvider),
            () => sync,
            () => new PartSyncService(api, new PartRepository(_userDatabase.Database), syncStates),
            _messenger,
            _timeProvider);
        _viewModel = new WorkOrderListViewModel(
            new ScreenState(formatter),
            _userDatabase.Database,
            _workOrders,
            _outbox,
            sync,
            coordinator,
            new WorkOrderListPresenter(new WorkOrderSectionClassifier(clock), formatter),
            _connectivity,
            _navigation,
            _ui,
            _messenger,
            _timeProvider);
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
    public async Task Should_Show_Local_Data_Without_Requests_When_Returning_Shortly_After_Pull()
    {
        await _viewModel.AppearCommand.ExecuteAsync(null);
        var requestsBefore = _handler.Requests.Count;
        var groups = _viewModel.Groups;
        await new OutboxWriter(_outbox, _timeProvider, _messenger).StartWorkAsync(_viewModel.Groups[0][0].Id);
        _timeProvider.Advance(WorkOrderListViewModel.RefreshOnReturnAfter - TimeSpan.FromSeconds(1));

        await _viewModel.AppearCommand.ExecuteAsync(null);

        _handler.Requests.Count.ShouldBe(requestsBefore);
        _viewModel.SyncStatus.ShouldBe("Do wysłania: 1");
        _viewModel.Groups.ShouldNotBeSameAs(groups);
    }

    [Fact]
    public async Task Should_Refresh_From_Api_When_Returning_After_Last_Pull_Got_Old()
    {
        await _viewModel.AppearCommand.ExecuteAsync(null);
        _timeProvider.Advance(WorkOrderListViewModel.RefreshOnReturnAfter);

        await _viewModel.AppearCommand.ExecuteAsync(null);

        _handler.Requests.Count(IsListRequest).ShouldBe(2);
    }

    [Fact]
    public async Task Should_Refresh_From_Api_When_List_Appears_For_Another_Technician()
    {
        await _viewModel.AppearCommand.ExecuteAsync(null);
        await _userDatabase.Database.CloseAsync();
        await _userDatabase.Database.OpenAsync(Guid.NewGuid());

        await _viewModel.AppearCommand.ExecuteAsync(null);

        _handler.Requests.Count(IsListRequest).ShouldBe(2);
    }

    [Fact]
    public async Task Should_Keep_Groups_When_Synchronization_Brings_No_Changes()
    {
        await _viewModel.AppearCommand.ExecuteAsync(null);
        var groups = _viewModel.Groups;

        _messenger.Send(new SyncCompletedMessage(new SyncResult(false, false, null, PullResult.Succeeded)));
        await Task.Delay(50, TestContext.Current.CancellationToken);

        _viewModel.Groups.ShouldBeSameAs(groups);
    }

    [Fact]
    public async Task Should_Show_Queued_Start_As_In_Progress_And_Pending_When_Device_Is_Offline()
    {
        var workOrder = TestWorkOrders.Create("ZL-LOCAL", Now.AddDays(1));
        await _workOrders.ReplaceAllAsync([workOrder]);
        await new OutboxWriter(_outbox, _timeProvider, _messenger).StartWorkAsync(workOrder.Id);
        _connectivity.IsOnline.Returns(false);

        await _viewModel.LoadCommand.ExecuteAsync(null);

        var item = _viewModel.Groups.SelectMany(group => group).ShouldHaveSingleItem();
        item.StatusTone.ShouldBe(BadgeTone.Warning);
        item.IsPending.ShouldBeTrue();
        _viewModel.SyncStatus.ShouldBe("Do wysłania: 1");
    }

    [Fact]
    public async Task Should_Show_Empty_State_When_Technician_Has_No_Work_Orders()
    {
        _remoteCount = 0;

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.Screen.Status.ShouldBe(ScreenStatus.Empty);
    }

    [Fact]
    public async Task Should_Show_Offline_Banner_With_Last_Sync_Without_Request_When_Device_Is_Offline()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _connectivity.IsOnline.Returns(false);
        var requestsBefore = _handler.Requests.Count;

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        _viewModel.Screen.IsOffline.ShouldBeTrue();
        _viewModel.Screen.OfflineMessage.ShouldNotBeNull().ShouldContain("05.10.2026 10:00");
        _viewModel.Screen.Status.ShouldBe(ScreenStatus.Content);
        _handler.Requests.Count.ShouldBe(requestsBefore);
    }

    [Fact]
    public async Task Should_Keep_Local_Data_And_Show_Notice_When_Refresh_Fails()
    {
        await _workOrders.ReplaceAllAsync([TestWorkOrders.Create("ZL-LOCAL", Now.AddDays(1))]);
        _respond = (request, _) => Task.FromResult(IsListRequest(request) ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Route(request));
        _viewModel.IsRefreshing = true;

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.Screen.Status.ShouldBe(ScreenStatus.Content);
        _viewModel.Notice.ShouldBe(AppResources.RefreshFailedNotice);
        _viewModel.IsRefreshing.ShouldBeFalse();
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
    public async Task Should_Show_Server_Starting_Notice_While_Server_Wakes_Up()
    {
        var ready = new TaskCompletionSource<HttpResponseMessage>();
        _respond = (request, cancellationToken) => request.RequestUri!.AbsolutePath == "/api/v1/system/ready"
            ? ready.Task.WaitAsync(cancellationToken)
            : Task.FromResult(Route(request));

        var load = _viewModel.LoadCommand.ExecuteAsync(null);
        for (var second = 0; second < 3; second++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            _timeProvider.Advance(TimeSpan.FromSeconds(1));
        }

        await Task.Delay(10, TestContext.Current.CancellationToken);
        _viewModel.Notice.ShouldBe(AppResources.ServerStartingNotice);
        ready.SetResult(new HttpResponseMessage(HttpStatusCode.OK));
        await load;
        _viewModel.Notice.ShouldBeNull();
        _viewModel.Screen.Status.ShouldBe(ScreenStatus.Content);
    }

    [Fact]
    public async Task Should_Update_Connection_Indicator_When_Connectivity_Changes()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);

        _messenger.Send(new ConnectivityChangedMessage(IsOnline: false));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        _viewModel.Screen.IsOffline.ShouldBeTrue();
        _viewModel.Screen.OfflineMessage.ShouldNotBeNull().ShouldContain("05.10.2026 10:00");

        _messenger.Send(new ConnectivityChangedMessage(IsOnline: true));
        _viewModel.Screen.IsOffline.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Open_Sync_Screen_When_Technician_Asks_For_Sync_Status()
    {
        await _viewModel.OpenSyncCommand.ExecuteAsync(null);

        await _navigation.Received(1).NavigateToAsync<SyncViewModel>();
    }

    [Fact]
    public async Task Should_Open_Details_When_Work_Order_Is_Selected()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);
        var item = _viewModel.Groups[0][0];
        _viewModel.SelectedWorkOrder = item;

        await _viewModel.OpenSelectedWorkOrderCommand.ExecuteAsync(null);

        await _navigation.Received(1).NavigateToAsync<WorkOrderDetailsViewModel, Guid>(item.Id);
        _viewModel.SelectedWorkOrder.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Open_Settings_When_Settings_Is_Selected()
    {
        await _viewModel.OpenSettingsCommand.ExecuteAsync(null);

        await _navigation.Received(1).NavigateToAsync<SettingsViewModel>();
    }

    [Fact]
    public async Task Should_Ignore_Synchronization_Messages_When_Technician_Signed_Out()
    {
        await _userDatabase.Database.DeleteAsync();

        _messenger.Send(new SyncCompletedMessage(SyncResult.NoSession));
        _messenger.Send(new ConnectivityChangedMessage(false));

        await Task.WhenAll(_ui.Posted);
        _viewModel.Screen.IsOffline.ShouldBeFalse();
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
            clientAddress = new { street = "Długa", buildingNumber = "12A", postalCode = "31-147", city = "Kraków" },
            clientContactPerson = "Anna Kowalska",
            clientPhone = "+48 600 100 200",
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
