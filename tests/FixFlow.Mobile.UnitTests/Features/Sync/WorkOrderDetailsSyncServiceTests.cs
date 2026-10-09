using System.Net;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.UnitTests.Api;
using FixFlow.Mobile.UnitTests.Features.WorkOrders;
using FixFlow.Mobile.UnitTests.Storage;
using Refit;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

public sealed class WorkOrderDetailsSyncServiceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DetailsApiStub.Now;

    private readonly TestUserDatabase _userDatabase = new();
    private readonly StubHttpMessageHandler _handler;
    private readonly WorkOrderRepository _workOrders;
    private readonly DeviceRepository _devices;
    private readonly ServiceEntryRepository _serviceEntries;
    private readonly DeviceHistoryRepository _deviceHistory;
    private readonly WorkOrderEventRepository _events;
    private readonly WorkOrderDetailsSyncService _service;
    private readonly DetailsApiStub _api = new();

    public WorkOrderDetailsSyncServiceTests()
    {
        _handler = new StubHttpMessageHandler(_api.Route);
        _workOrders = new WorkOrderRepository(_userDatabase.Database);
        _devices = new DeviceRepository(_userDatabase.Database);
        _serviceEntries = new ServiceEntryRepository(_userDatabase.Database);
        _deviceHistory = new DeviceHistoryRepository(_userDatabase.Database);
        _events = new WorkOrderEventRepository(_userDatabase.Database);
        _service = new WorkOrderDetailsSyncService(
            RestService.ForGenerated<IFixFlowApi>(new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") }),
            _workOrders,
            _devices,
            _serviceEntries,
            _deviceHistory,
            _events);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Store_Work_Order_With_Client_Contact_Device_Entries_And_History_When_Pull_Succeeds()
    {
        var result = await PullAsync();

        result.ShouldBe(PullResult.Succeeded);
        var workOrder = (await _workOrders.GetAsync(_api.WorkOrderId)).ShouldNotBeNull();
        workOrder.Number.ShouldBe("ZL-2026-0042");
        workOrder.ClientContact.City.ShouldBe("Kraków");
        workOrder.ClientSignaturePhotoId.ShouldBe(_api.SignatureId);
        _handler.Requests.ShouldNotContain(request => request.RequestUri!.AbsolutePath.StartsWith("/api/v1/clients", StringComparison.Ordinal));
        (await _devices.GetAsync(_api.DeviceId)).ShouldNotBeNull().InstallationDate.ShouldBe(new DateOnly(2021, 3, 14));
        var entry = (await _serviceEntries.GetForWorkOrderAsync(_api.WorkOrderId)).ShouldHaveSingleItem();
        entry.Parts.ShouldHaveSingleItem().UnitPrice.ShouldBe(12.34m);
        (await _deviceHistory.GetForDeviceAsync(_api.DeviceId)).ShouldHaveSingleItem().ServiceEntryCount.ShouldBe(3);
        var events = await _events.GetForWorkOrderAsync(_api.WorkOrderId);
        events.Select(item => item.Type).ShouldBe([WorkOrderEventType.Created, WorkOrderEventType.Assigned]);
        events[1].ActorName.ShouldBe("Anna Nowak");
    }

    [Fact]
    public async Task Should_Remove_Local_Work_Order_When_Api_Returns_404_After_Reassignment()
    {
        await PullAsync();
        _api.Failures["/api/v1/work-orders/" + _api.WorkOrderId] = HttpStatusCode.NotFound;

        var result = await PullAsync();

        result.ShouldBe(PullResult.NotFound);
        (await _workOrders.GetAsync(_api.WorkOrderId)).ShouldBeNull();
        (await _serviceEntries.GetForWorkOrderAsync(_api.WorkOrderId)).ShouldBeEmpty();
        (await _events.GetForWorkOrderAsync(_api.WorkOrderId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Keep_Local_Work_Order_When_Api_Is_Unavailable()
    {
        var local = TestWorkOrders.Create("ZL-LOCAL", Now) with { Id = _api.WorkOrderId };
        await _workOrders.ReplaceAllAsync([local]);
        _api.Failures["/api/v1/work-orders/" + _api.WorkOrderId] = HttpStatusCode.ServiceUnavailable;

        var result = await PullAsync();

        result.Outcome.ShouldBe(PullOutcome.Failed);
        (await _workOrders.GetAsync(_api.WorkOrderId)).ShouldBe(local);
    }

    [Fact]
    public async Task Should_Save_Available_Details_And_Report_Failure_When_One_Request_Fails()
    {
        _api.Failures[$"/api/v1/devices/{_api.DeviceId}/work-orders"] = HttpStatusCode.ServiceUnavailable;

        var result = await PullAsync();

        result.Outcome.ShouldBe(PullOutcome.Failed);
        (await _devices.GetAsync(_api.DeviceId)).ShouldNotBeNull();
        (await _serviceEntries.GetForWorkOrderAsync(_api.WorkOrderId)).ShouldNotBeEmpty();
        (await _deviceHistory.GetForDeviceAsync(_api.DeviceId)).ShouldBeEmpty();
    }

    public async ValueTask DisposeAsync()
    {
        _handler.Dispose();
        await _userDatabase.DisposeAsync();
    }

    private Task<PullResult> PullAsync() => _service.PullAsync(_api.WorkOrderId, TestContext.Current.CancellationToken);
}
