using System.Net;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Launching;
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
    private readonly StubHttpMessageHandler _handler;
    private readonly WorkOrderDetailsSources _sources;
    private readonly WorkOrderDetailsViewModel _viewModel;

    public WorkOrderDetailsViewModelTests()
    {
        _connectivity.IsOnline.Returns(true);
        _handler = new StubHttpMessageHandler(request => _api.Route(request));
        var timeProvider = new FakeTimeProvider(DetailsApiStub.Now);
        var database = _userDatabase.Database;
        _sources = new WorkOrderDetailsSources(
            new WorkOrderRepository(database),
            new ClientRepository(database),
            new DeviceRepository(database),
            new ServiceEntryRepository(database),
            new DeviceHistoryRepository(database));
        var api = RestService.ForGenerated<IFixFlowApi>(new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") });
        var formatter = new DisplayFormatter(new LanguageService(Substitute.For<ISettingsStore>()), BusinessClock.CreateForWarsaw(timeProvider));
        _viewModel = new WorkOrderDetailsViewModel(
            new ScreenState(formatter),
            _sources,
            new WorkOrderDetailsSyncService(api, _sources.WorkOrders, _sources.Clients, _sources.Devices, _sources.ServiceEntries, _sources.DeviceHistory),
            new SyncStateRepository(database, timeProvider),
            new WorkOrderDetailsPresenter(formatter),
            new ServiceProtocolService(api, _documents),
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
    public async Task Should_Show_Local_Details_Without_Request_When_Device_Is_Offline()
    {
        await _sources.WorkOrders.ReplaceAllAsync([TestWorkOrders.Create("ZL-LOCAL", DetailsApiStub.Now) with { Id = _api.WorkOrderId }]);
        _connectivity.IsOnline.Returns(false);

        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);

        _viewModel.Details.ShouldNotBeNull().Number.ShouldBe("ZL-LOCAL");
        _viewModel.Screen.IsOffline.ShouldBeTrue();
        _handler.Requests.ShouldBeEmpty();
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
        _viewModel.Notice.ShouldBe(AppResources.DetailsRefreshFailed);
    }

    [Fact]
    public async Task Should_Keep_Local_Details_And_Show_Notice_When_Refresh_Fails()
    {
        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);
        _api.Failures["/api/v1/work-orders/" + _api.WorkOrderId] = HttpStatusCode.ServiceUnavailable;

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        _viewModel.Details.ShouldNotBeNull();
        _viewModel.Notice.ShouldBe(AppResources.DetailsRefreshFailed);
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
    public async Task Should_Navigate_To_Previous_Work_Order_When_History_Item_Is_Selected()
    {
        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);
        var item = _viewModel.Details!.History[0];

        await _viewModel.OpenHistoryItemCommand.ExecuteAsync(item);

        await _navigation.Received(1).NavigateToAsync<WorkOrderDetailsViewModel, Guid>(item.WorkOrderId);
    }

    public async ValueTask DisposeAsync()
    {
        _handler.Dispose();
        await _userDatabase.DisposeAsync();
    }
}
