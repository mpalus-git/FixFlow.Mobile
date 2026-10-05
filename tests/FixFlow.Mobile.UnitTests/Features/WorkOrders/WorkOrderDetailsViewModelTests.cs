using System.Net;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
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
            _connectivity);
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
    public async Task Should_Keep_Local_Details_And_Show_Notice_When_Refresh_Fails()
    {
        await _viewModel.OnNavigatedToAsync(_api.WorkOrderId);
        _api.Failures["/api/v1/work-orders/" + _api.WorkOrderId] = HttpStatusCode.ServiceUnavailable;

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        _viewModel.Details.ShouldNotBeNull();
        _viewModel.Notice.ShouldBe(AppResources.DetailsRefreshFailed);
    }

    public async ValueTask DisposeAsync()
    {
        _handler.Dispose();
        await _userDatabase.DisposeAsync();
    }
}
