using System.Net;
using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Accessibility;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Launching;
using FixFlow.Mobile.Core.Location;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.Core.Screens;
using FixFlow.Mobile.UnitTests.Api;
using FixFlow.Mobile.UnitTests.Features.Sync;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Refit;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class WorkOrderDetailsViewModelTests : IAsyncLifetime
{
    private readonly DetailsApiStub _api = new();
    private readonly TestUserDatabase _userDatabase = new();
    private readonly IConnectivityService _connectivity = Substitute.For<IConnectivityService>();
    private readonly IMapLauncher _maps = Substitute.For<IMapLauncher>();
    private readonly IUriLauncher _uris = Substitute.For<IUriLauncher>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IDocumentLauncher _documents = Substitute.For<IDocumentLauncher>();
    private readonly ILocationService _location = Substitute.For<ILocationService>();
    private readonly FakeTimeProvider _timeProvider = new(DetailsApiStub.Now);
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly StubHttpMessageHandler _handler;
    private readonly WorkOrderDetailsSources _sources;
    private readonly WorkOrderDetailsViewModel _viewModel;

    public WorkOrderDetailsViewModelTests()
    {
        _connectivity.IsOnline.Returns(true);
        _handler = new StubHttpMessageHandler(request => _api.Route(request));
        var timeProvider = _timeProvider;
        var database = _userDatabase.Database;
        _sources = new WorkOrderDetailsSources(
            new WorkOrderRepository(database),
            new DeviceRepository(database),
            new ServiceEntryRepository(database),
            new DeviceHistoryRepository(database),
            new OutboxRepository(database),
            new WorkTimerRepository(database),
            new WorkOrderEventRepository(database));
        var api = RestService.ForGenerated<IFixFlowApi>(new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") });
        var formatter = new DisplayFormatter(new LanguageService(Substitute.For<ISettingsStore>()), BusinessClock.CreateForWarsaw(timeProvider));
        _viewModel = new WorkOrderDetailsViewModel(
            new ScreenState(formatter, Substitute.For<IScreenReaderAnnouncer>()),
            _sources,
            new WorkOrderDetailsSyncService(api, _sources.WorkOrders, _sources.Devices, _sources.ServiceEntries, _sources.DeviceHistory, _sources.WorkOrderEvents),
            new SyncStateRepository(database, timeProvider),
            new WorkOrderDetailsPresenter(formatter, new WorkOrderSectionClassifier(BusinessClock.CreateForWarsaw(timeProvider)), new SessionStore(Substitute.For<ISecureStore>(), timeProvider), new ApiAddress(new Uri("https://api.test/"))),
            new ServiceProtocolService(api, _documents),
            new OutboxWriter(_sources.Outbox, timeProvider, new WeakReferenceMessenger()),
            new WorkLocationRecorder(_location, _dialogs, _sources.WorkTimers),
            _location,
            timeProvider,
            new ImmediateUiDispatcher(),
            _messenger,
            new PhotoStore(_userDatabase.Options, database),
            _documents,
            _connectivity,
            _maps,
            _uris,
            _dialogs,
            _navigation);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Show_Details_With_Client_Device_And_Entries_When_Pulled_From_Api()
    {
        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);

        _viewModel.Screen.Status.ShouldBe(ScreenStatus.Content);
        var details = _viewModel.Details.ShouldNotBeNull();
        details.Number.ShouldBe("ZL-2026-0042");
        details.Client.ShouldNotBeNull().AddressText.ShouldBe("Długa 12A, 31-147 Kraków");
        details.Entries.ShouldHaveSingleItem().Parts.ShouldHaveSingleItem();
        details.History.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Should_Show_Local_Details_With_Client_Contact_From_List_When_Device_Is_Offline()
    {
        await _sources.WorkOrders.ReplaceAllAsync([TestWorkOrders.Create("ZL-LOCAL", DetailsApiStub.Now) with
        {
            Id = _api.WorkOrderId,
            ClientContact = new ClientContact("Długa", "12A", "31-147", "Kraków", "Anna Kowalska", "+48 600 100 200"),
        }]);
        _connectivity.IsOnline.Returns(false);

        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);

        _viewModel.Details.ShouldNotBeNull().Number.ShouldBe("ZL-LOCAL");
        _viewModel.Details.Client.ShouldNotBeNull().Phone.ShouldBe("+48 600 100 200");
        _viewModel.Screen.IsOffline.ShouldBeTrue();
        _handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Show_Queued_Start_And_Entry_When_Device_Is_Offline()
    {
        await _sources.WorkOrders.ReplaceAllAsync([TestWorkOrders.Create("ZL-LOCAL", DetailsApiStub.Now) with { Id = _api.WorkOrderId }]);
        var writer = new OutboxWriter(_sources.Outbox, TimeProvider.System, new WeakReferenceMessenger());
        await writer.StartWorkAsync(_api.WorkOrderId);
        await writer.AddServiceEntryAsync(_api.WorkOrderId, new PendingServiceEntry(Guid.CreateVersion7(), "Offline", false, null, null, null, null, []));
        _connectivity.IsOnline.Returns(false);

        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);

        var details = _viewModel.Details.ShouldNotBeNull();
        details.StatusText.ShouldBe(WorkOrderTexts.Status(WorkOrderStatus.InProgress));
        details.Entries.ShouldHaveSingleItem().IsPending.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Point_To_Sync_Screen_When_Change_Of_Work_Order_Was_Rejected()
    {
        await _sources.WorkOrders.ReplaceAllAsync([TestWorkOrders.Create("ZL-LOCAL", DetailsApiStub.Now) with { Id = _api.WorkOrderId }]);
        var start = await new OutboxWriter(_sources.Outbox, TimeProvider.System, new WeakReferenceMessenger()).StartWorkAsync(_api.WorkOrderId);
        await _sources.Outbox.MoveToConflictsAsync(new SyncConflict(
            start.Id, start.WorkOrderId, start.Kind, start.Payload, start.CreatedAt, HttpStatusCode.Conflict, "WorkOrder.TechnicianAlreadyHasWorkInProgress", null, DetailsApiStub.Now));
        _connectivity.IsOnline.Returns(false);
        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);

        await _viewModel.OpenSyncCommand.ExecuteAsync(null);

        _viewModel.HasRejectedChanges.ShouldBeTrue();
        await _navigation.Received(1).NavigateToAsync<SyncViewModel>();
    }

    [Fact]
    public async Task Should_Explain_And_Hide_Details_When_Work_Order_Was_Reassigned()
    {
        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);
        _api.Failures["/api/v1/work-orders/" + _api.WorkOrderId] = HttpStatusCode.NotFound;

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        _viewModel.Details.ShouldBeNull();
        _viewModel.Screen.Status.ShouldBe(ScreenStatus.Error);
        _viewModel.Screen.ErrorMessage.ShouldBe(AppResources.WorkOrderNoLongerAssigned);
    }

    [Fact]
    public async Task Should_Show_Saved_Client_And_Notice_When_Only_Device_History_Fails()
    {
        _api.Failures[$"/api/v1/devices/{_api.DeviceId}/work-orders"] = HttpStatusCode.NotFound;

        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);

        _viewModel.Details.ShouldNotBeNull().Client.ShouldNotBeNull();
        _viewModel.Screen.Notice.ShouldBe(ScreenNotice.Problem(AppResources.DetailsRefreshFailed));
    }

    [Fact]
    public async Task Should_Keep_Local_Details_And_Show_Notice_When_Refresh_Fails()
    {
        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);
        _api.Failures["/api/v1/work-orders/" + _api.WorkOrderId] = HttpStatusCode.ServiceUnavailable;

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        _viewModel.Details.ShouldNotBeNull();
        _viewModel.Screen.Notice.ShouldBe(ScreenNotice.Problem(AppResources.DetailsRefreshFailed));
    }

    [Fact]
    public async Task Should_Dial_Phone_Number_Without_Spaces_When_Technician_Calls_Client()
    {
        _uris.OpenAsync(Arg.Any<Uri>()).Returns(true);
        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);

        await _viewModel.CallClientCommand.ExecuteAsync(null);

        await _uris.Received(1).OpenAsync(new Uri("tel:+48600100200"));
    }

    [Fact]
    public async Task Should_Open_Client_Address_And_Report_Failure_When_No_Map_App_Is_Available()
    {
        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);

        await _viewModel.OpenAddressCommand.ExecuteAsync(null);

        await _maps.Received(1).OpenAddressAsync("Piekarnia", "Długa 12A", "31-147", "Kraków");
        await _dialogs.Received(1).ShowAlertAsync(AppResources.ErrorTitle, AppResources.MapUnavailable, AppResources.CloseButton);
    }

    [Fact]
    public async Task Should_Open_Gps_Position_When_Entry_Has_Location()
    {
        _maps.OpenLocationAsync(Arg.Any<string>(), Arg.Any<double>(), Arg.Any<double>()).Returns(true);
        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);

        await _viewModel.OpenEntryLocationCommand.ExecuteAsync(_viewModel.Details!.Entries[0]);

        await _maps.Received(1).OpenLocationAsync("ZL-2026-0042", 50.0614, 19.9366);
    }

    [Fact]
    public async Task Should_Open_Protocol_When_Work_Order_Is_Completed()
    {
        _api.Status = "Completed";
        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);

        await _viewModel.DownloadProtocolCommand.ExecuteAsync(null);

        await _documents.Received(1).OpenAsync(Arg.Any<Stream>(), "protokol-ZL-2026-0042.pdf", "application/pdf");
    }

    [Fact]
    public async Task Should_Explain_Without_Request_When_Protocol_Is_Requested_Offline()
    {
        _api.Status = "Completed";
        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);
        _connectivity.IsOnline.Returns(false);
        var requestsBefore = _handler.Requests.Count;

        await _viewModel.DownloadProtocolCommand.ExecuteAsync(null);

        _handler.Requests.Count.ShouldBe(requestsBefore);
        await _dialogs.Received(1).ShowAlertAsync(AppResources.ErrorTitle, AppResources.ProtocolRequiresConnection, AppResources.CloseButton);
    }

    [Fact]
    public async Task Should_Open_Sent_Photo_Address_And_Explain_When_No_App_Can_Show_It()
    {
        await _viewModel.OpenPhotoCommand.ExecuteAsync(new EntryPhotoView("https://api.test/api/v1/photos/1", false));

        await _uris.Received(1).OpenAsync(new Uri("https://api.test/api/v1/photos/1"));
        await _dialogs.Received(1).ShowAlertAsync(AppResources.ErrorTitle, AppResources.PhotoUnavailable, AppResources.CloseButton);
    }

    [Fact]
    public async Task Should_Navigate_To_Previous_Work_Order_When_History_Item_Is_Selected()
    {
        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);
        var item = _viewModel.Details!.History[0];

        await _viewModel.OpenHistoryItemCommand.ExecuteAsync(item);

        await _navigation.Received(1).NavigateToAsync<WorkOrderDetailsViewModel, Guid>(item.WorkOrderId);
    }

    [Fact]
    public async Task Should_Offer_Start_When_Work_Order_Is_Assigned_And_No_Other_Is_In_Progress()
    {
        await ShowOfflineAsync(TestWorkOrders.Create("ZL-2", DetailsApiStub.Now, WorkOrderStatus.Completed, DetailsApiStub.Now));

        _viewModel.Actions.CanStartWork.ShouldBeTrue();
        _viewModel.Actions.HasStartBlockedText.ShouldBeFalse();
        _viewModel.HasWorkTimer.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Explain_Block_When_Other_Work_Order_Is_In_Progress()
    {
        await ShowOfflineAsync(TestWorkOrders.Create("ZL-2", DetailsApiStub.Now, WorkOrderStatus.InProgress));

        _viewModel.Actions.CanStartWork.ShouldBeFalse();
        _viewModel.Actions.StartBlockedText.ShouldBe(AppText.Format(AppResources.StartWorkBlocked, "ZL-2"));
    }

    [Fact]
    public async Task Should_Not_Queue_Start_When_Technician_Cancels_Confirmation()
    {
        await ShowOfflineAsync();

        await _viewModel.StartWorkCommand.ExecuteAsync(null);

        (await _sources.Outbox.GetAllAsync()).ShouldBeEmpty();
        _viewModel.Actions.CanStartWork.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Queue_Start_And_Show_Timer_With_Position_When_Technician_Confirms()
    {
        ConfirmStart();
        _location.HasPermissionAsync().Returns(true);
        _location.GetCurrentPositionAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(new GeoPosition(50.06, 19.94));
        await ShowOfflineAsync();

        await _viewModel.StartWorkCommand.ExecuteAsync(null);

        (await _sources.Outbox.GetAllAsync()).ShouldHaveSingleItem().Kind.ShouldBe(OutboxOperationKind.StartWork);
        _viewModel.Details.ShouldNotBeNull().StatusText.ShouldBe(WorkOrderTexts.Status(WorkOrderStatus.InProgress));
        _viewModel.Actions.CanStartWork.ShouldBeFalse();
        var timer = _viewModel.WorkTimer.ShouldNotBeNull();
        timer.LocationText.ShouldBe(AppResources.WorkLocationCaptured);
        timer.CanRetryLocation.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Start_Work_Without_Position_And_Offer_Settings_When_Location_Is_Denied()
    {
        ConfirmStart();
        await ShowOfflineAsync();

        await _viewModel.StartWorkCommand.ExecuteAsync(null);
        await _viewModel.OpenLocationSettingsCommand.ExecuteAsync(null);

        (await _sources.Outbox.GetAllAsync()).ShouldHaveSingleItem();
        var timer = _viewModel.WorkTimer.ShouldNotBeNull();
        timer.LocationText.ShouldBe(AppResources.WorkLocationDenied);
        timer.CanOpenLocationSettings.ShouldBeTrue();
        await _location.Received(1).OpenSettingsAsync();
    }

    [Fact]
    public async Task Should_Record_Position_When_Technician_Retries_After_Failure()
    {
        ConfirmStart();
        _location.HasPermissionAsync().Returns(true);
        await ShowOfflineAsync();
        await _viewModel.StartWorkCommand.ExecuteAsync(null);
        _location.GetCurrentPositionAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(new GeoPosition(50.06, 19.94));

        await _viewModel.RetryLocationCommand.ExecuteAsync(null);

        _viewModel.WorkTimer.ShouldNotBeNull().LocationText.ShouldBe(AppResources.WorkLocationCaptured);
    }

    [Fact]
    public async Task Should_Update_Elapsed_Time_When_Clock_Ticks()
    {
        ConfirmStart();
        await ShowOfflineAsync();
        await _viewModel.StartWorkCommand.ExecuteAsync(null);
        _viewModel.Resume();

        _timeProvider.Advance(TimeSpan.FromMinutes(90));

        _viewModel.WorkTimer.ShouldNotBeNull().ElapsedText.ShouldBe(
            AppText.Format(AppResources.WorkTimerElapsed, AppText.Format(AppResources.DetailsDuration, 1, 30)));
        _viewModel.Pause();
    }

    [Fact]
    public async Task Should_Offer_Entry_Form_When_Work_Order_Is_In_Progress()
    {
        ConfirmStart();
        await ShowOfflineAsync();
        _viewModel.Actions.CanAddEntry.ShouldBeFalse();
        await _viewModel.StartWorkCommand.ExecuteAsync(null);

        await _viewModel.AddEntryCommand.ExecuteAsync(null);

        _viewModel.Actions.CanAddEntry.ShouldBeTrue();
        await _navigation.Received(1).NavigateToAsync<ServiceEntryViewModel, Guid>(_api.WorkOrderId);
    }

    [Fact]
    public async Task Should_Require_Entry_Before_Offering_Completion()
    {
        ConfirmStart();
        await ShowOfflineAsync();

        await _viewModel.StartWorkCommand.ExecuteAsync(null);

        _viewModel.Actions.CanComplete.ShouldBeFalse();
        _viewModel.Actions.CompleteBlockedText.ShouldBe(AppResources.CompleteRequiresEntry);
        _viewModel.Actions.CanAddCorrection.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Open_Completion_Screen_When_Work_Order_Can_Be_Completed()
    {
        ConfirmStart();
        await ShowOfflineAsync();
        await _viewModel.StartWorkCommand.ExecuteAsync(null);
        await AddPendingEntryAsync(new PendingServiceEntryPart(Guid.NewGuid(), "Filtr", "FP-100", 1, 12m));

        await _viewModel.CompleteCommand.ExecuteAsync(null);

        await _navigation.Received(1).NavigateToAsync<CompleteWorkOrderViewModel, Guid>(_api.WorkOrderId);
        (await _sources.Outbox.GetAllAsync()).ShouldNotContain(operation => operation.Kind == OutboxOperationKind.CompleteWorkOrder);
    }

    [Fact]
    public async Task Should_Show_Completed_Work_Order_Without_Timer_When_Completion_Waits_In_Queue()
    {
        ConfirmStart();
        await ShowOfflineAsync();
        await _viewModel.StartWorkCommand.ExecuteAsync(null);
        await AddPendingEntryAsync();
        _viewModel.Pause();
        await new OutboxWriter(_sources.Outbox, _timeProvider, new WeakReferenceMessenger()).CompleteWorkOrderAsync(_api.WorkOrderId);

        _viewModel.Resume();
        await WaitUntilAsync(() => _viewModel.Details?.StatusText == WorkOrderTexts.Status(WorkOrderStatus.Completed));

        _viewModel.Actions.CanComplete.ShouldBeFalse();
        _viewModel.Actions.CanAddEntry.ShouldBeFalse();
        _viewModel.HasWorkTimer.ShouldBeFalse();
        _viewModel.Pause();
    }

    [Fact]
    public async Task Should_Show_Entry_Saved_Elsewhere_When_Page_Is_Shown_Again()
    {
        ConfirmStart();
        await ShowOfflineAsync();
        await _viewModel.StartWorkCommand.ExecuteAsync(null);
        _viewModel.Pause();
        await new OutboxWriter(_sources.Outbox, _timeProvider, new WeakReferenceMessenger()).AddServiceEntryAsync(
            _api.WorkOrderId, new PendingServiceEntry(Guid.CreateVersion7(), "Z formularza", false, null, null, null, null, []));

        _viewModel.Resume();
        await WaitUntilAsync(() => _viewModel.Details?.HasEntries == true);

        _viewModel.Details!.Entries.ShouldHaveSingleItem().Note.ShouldBe("Z formularza");
        _viewModel.Pause();
    }

    [Fact]
    public async Task Should_Reload_Local_Details_When_Synchronization_Completes_While_Page_Is_Visible()
    {
        await ShowOfflineAsync();
        _viewModel.Resume();
        await new OutboxWriter(_sources.Outbox, _timeProvider, new WeakReferenceMessenger()).StartWorkAsync(_api.WorkOrderId);

        _messenger.Send(new SyncCompletedMessage(SyncResult.Offline));
        await WaitUntilAsync(() => _viewModel.Actions.CanAddEntry);

        _viewModel.Details!.StatusText.ShouldBe(WorkOrderTexts.Status(WorkOrderStatus.InProgress));
        _viewModel.Pause();
    }

    [Fact]
    public async Task Should_Clear_Offline_Banner_When_Connection_Returns()
    {
        await ShowOfflineAsync();
        _viewModel.Resume();

        _messenger.Send(new ConnectivityChangedMessage(true));
        await WaitUntilAsync(() => !_viewModel.Screen.IsOffline);

        _viewModel.Screen.IsOffline.ShouldBeFalse();
        _viewModel.Pause();
    }

    [Fact]
    public async Task Should_Show_Offline_Banner_When_Connection_Is_Lost()
    {
        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);
        _viewModel.Resume();

        _messenger.Send(new ConnectivityChangedMessage(false));
        await WaitUntilAsync(() => _viewModel.Screen.IsOffline);

        _viewModel.Screen.IsOffline.ShouldBeTrue();
        _viewModel.Pause();
    }

    [Fact]
    public async Task Should_Clear_Refresh_Notice_When_Background_Sync_Succeeds()
    {
        _api.Failures[$"/api/v1/devices/{_api.DeviceId}/work-orders"] = HttpStatusCode.ServiceUnavailable;
        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);
        _viewModel.Screen.Notice.ShouldNotBeNull();
        _api.Failures.Clear();
        _viewModel.Resume();

        _messenger.Send(new SyncCompletedMessage(new SyncResult(false, false, null, PullResult.Succeeded)));
        await WaitUntilAsync(() => _viewModel.Screen.Notice is null);

        _viewModel.Screen.Notice.ShouldBeNull();
        _viewModel.Pause();
    }

    [Fact]
    public async Task Should_Pull_Details_Again_When_Background_Sync_Sent_Pending_Changes()
    {
        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);
        var start = await new OutboxWriter(_sources.Outbox, _timeProvider, new WeakReferenceMessenger()).StartWorkAsync(_api.WorkOrderId);
        _viewModel.Resume();
        await WaitUntilAsync(() => _viewModel.Details?.Events.Any(item => item.IsPending) == true);
        await _sources.Outbox.RemoveAsync(start.Id);

        _messenger.Send(new SyncCompletedMessage(new SyncResult(false, false, new DispatchSummary(1, 0, 0, false), PullResult.Succeeded)));
        await WaitUntilAsync(() => EventRequests() == 2);

        EventRequests().ShouldBe(2);
        _viewModel.Pause();
    }

    [Fact]
    public async Task Should_Ignore_Connection_Changes_When_Page_Is_Hidden()
    {
        await ShowOfflineAsync();
        _viewModel.Resume();
        _viewModel.Pause();

        _messenger.Send(new ConnectivityChangedMessage(true));

        _viewModel.Screen.IsOffline.ShouldBeTrue();
    }

    public async ValueTask DisposeAsync()
    {
        _handler.Dispose();
        await _userDatabase.DisposeAsync();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private async Task AddPendingEntryAsync(params PendingServiceEntryPart[] parts)
    {
        await new OutboxWriter(_sources.Outbox, _timeProvider, new WeakReferenceMessenger()).AddServiceEntryAsync(
            _api.WorkOrderId, new PendingServiceEntry(Guid.CreateVersion7(), "Przegląd", false, null, null, null, null, parts));
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void ConfirmStart() =>
        _dialogs.ConfirmAsync(AppResources.StartWorkConfirmTitle, Arg.Any<string>(), AppResources.StartWorkButton, AppResources.CancelButton).Returns(true);

    private int EventRequests() =>
        _handler.Requests.Count(request => request.RequestUri!.AbsolutePath == $"/api/v1/work-orders/{_api.WorkOrderId}/events");

    private async Task ShowOfflineAsync(params WorkOrder[] otherWorkOrders)
    {
        await _sources.WorkOrders.ReplaceAllAsync([TestWorkOrders.Create("ZL-LOCAL", DetailsApiStub.Now) with { Id = _api.WorkOrderId }, .. otherWorkOrders]);
        _connectivity.IsOnline.Returns(false);
        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);
    }
}
