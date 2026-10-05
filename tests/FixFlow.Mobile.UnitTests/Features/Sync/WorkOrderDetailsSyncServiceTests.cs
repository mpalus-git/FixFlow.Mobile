using System.Net;
using System.Text;
using System.Text.Json;
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
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkOrderId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly Guid DeviceId = Guid.NewGuid();

    private readonly TestUserDatabase _userDatabase = new();
    private readonly StubHttpMessageHandler _handler;
    private readonly WorkOrderRepository _workOrders;
    private readonly ClientRepository _clients;
    private readonly DeviceRepository _devices;
    private readonly ServiceEntryRepository _serviceEntries;
    private readonly DeviceHistoryRepository _deviceHistory;
    private readonly WorkOrderDetailsSyncService _service;
    private readonly Dictionary<string, HttpStatusCode> _failures = [];

    public WorkOrderDetailsSyncServiceTests()
    {
        _handler = new StubHttpMessageHandler(Route);
        _workOrders = new WorkOrderRepository(_userDatabase.Database);
        _clients = new ClientRepository(_userDatabase.Database);
        _devices = new DeviceRepository(_userDatabase.Database);
        _serviceEntries = new ServiceEntryRepository(_userDatabase.Database);
        _deviceHistory = new DeviceHistoryRepository(_userDatabase.Database);
        _service = new WorkOrderDetailsSyncService(
            RestService.ForGenerated<IFixFlowApi>(new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") }),
            _workOrders,
            _clients,
            _devices,
            _serviceEntries,
            _deviceHistory);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Store_Work_Order_With_Client_Device_Entries_And_History_When_Pull_Succeeds()
    {
        var result = await PullAsync();

        result.ShouldBe(PullResult.Succeeded);
        (await _workOrders.GetAsync(WorkOrderId)).ShouldNotBeNull().Number.ShouldBe("ZL-2026-0042");
        (await _clients.GetAsync(ClientId)).ShouldNotBeNull().City.ShouldBe("Kraków");
        (await _devices.GetAsync(DeviceId)).ShouldNotBeNull().InstallationDate.ShouldBe(new DateOnly(2021, 3, 14));
        var entry = (await _serviceEntries.GetForWorkOrderAsync(WorkOrderId)).ShouldHaveSingleItem();
        entry.Parts.ShouldHaveSingleItem().UnitPrice.ShouldBe(12.34m);
        (await _deviceHistory.GetForDeviceAsync(DeviceId)).ShouldHaveSingleItem().ServiceEntryCount.ShouldBe(3);
    }

    [Fact]
    public async Task Should_Remove_Local_Work_Order_When_Api_Returns_404_After_Reassignment()
    {
        await PullAsync();
        _failures["/api/v1/work-orders/" + WorkOrderId] = HttpStatusCode.NotFound;

        var result = await PullAsync();

        result.ShouldBe(PullResult.NotFound);
        (await _workOrders.GetAsync(WorkOrderId)).ShouldBeNull();
        (await _serviceEntries.GetForWorkOrderAsync(WorkOrderId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Keep_Local_Work_Order_When_Api_Is_Unavailable()
    {
        var local = TestWorkOrders.Create("ZL-LOCAL", Now) with { Id = WorkOrderId };
        await _workOrders.ReplaceAllAsync([local]);
        _failures["/api/v1/work-orders/" + WorkOrderId] = HttpStatusCode.ServiceUnavailable;

        var result = await PullAsync();

        result.Outcome.ShouldBe(PullOutcome.Failed);
        (await _workOrders.GetAsync(WorkOrderId)).ShouldBe(local);
    }

    [Fact]
    public async Task Should_Save_Available_Details_And_Report_Failure_When_One_Request_Fails()
    {
        _failures[$"/api/v1/devices/{DeviceId}/work-orders"] = HttpStatusCode.ServiceUnavailable;

        var result = await PullAsync();

        result.Outcome.ShouldBe(PullOutcome.Failed);
        (await _clients.GetAsync(ClientId)).ShouldNotBeNull();
        (await _serviceEntries.GetForWorkOrderAsync(WorkOrderId)).ShouldNotBeEmpty();
        (await _deviceHistory.GetForDeviceAsync(DeviceId)).ShouldBeEmpty();
    }

    public async ValueTask DisposeAsync()
    {
        _handler.Dispose();
        await _userDatabase.DisposeAsync();
    }

    private Task<PullResult> PullAsync() => _service.PullAsync(WorkOrderId, TestContext.Current.CancellationToken);

    private HttpResponseMessage Route(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (_failures.TryGetValue(path, out var status))
        {
            return new HttpResponseMessage(status)
            {
                Content = new StringContent("""{"status":404,"errorCode":"WorkOrder.NotFound"}""", Encoding.UTF8, "application/problem+json"),
            };
        }

        object content = path switch
        {
            _ when path == $"/api/v1/work-orders/{WorkOrderId}" => new
            {
                id = WorkOrderId, number = "ZL-2026-0042", deviceId = DeviceId, deviceSerialNumber = "SN-1", deviceModel = "Altherma 3",
                clientId = ClientId, clientName = "Piekarnia", description = "Przegląd", priority = "High", status = "InProgress",
                dueDate = Now, isOverdue = false, createdAt = Now.AddDays(-2), startedAt = Now.AddHours(-1),
            },
            _ when path == $"/api/v1/clients/{ClientId}" => new
            {
                id = ClientId, name = "Piekarnia", contactPerson = "Anna Kowalska", phone = "+48 600 100 200", createdAt = Now,
                address = new { street = "Długa", buildingNumber = "12A", postalCode = "31-147", city = "Kraków" },
            },
            _ when path == $"/api/v1/devices/{DeviceId}" => new
            {
                id = DeviceId, clientId = ClientId, serialNumber = "SN-1", model = "Altherma 3", manufacturer = "Daikin",
                installationDate = "2021-03-14", createdAt = Now,
            },
            _ when path == $"/api/v1/work-orders/{WorkOrderId}/service-entries" => new[]
            {
                new
                {
                    id = Guid.NewGuid(), workOrderId = WorkOrderId, technicianId = Guid.NewGuid(), technicianName = "Jan Kowalski",
                    note = "Wymiana filtra", isCorrection = false, photoUrls = Array.Empty<string>(), createdAt = Now,
                    parts = new[] { new { partId = Guid.NewGuid(), partName = "Filtr", catalogNumber = "FP-100", quantity = 2, unitPrice = 12.34m } },
                },
            },
            _ when path == $"/api/v1/devices/{DeviceId}/work-orders" => new
            {
                page = 1, pageSize = 100, totalCount = 1,
                items = new[]
                {
                    new
                    {
                        id = Guid.NewGuid(), number = "ZL-2026-0007", status = "Invoiced", priority = "Normal", description = "Montaż",
                        technicianName = "Jan Kowalski", createdAt = Now.AddDays(-90), serviceEntryCount = 3,
                    },
                },
            },
            _ => throw new InvalidOperationException($"Unexpected request {path}"),
        };

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json"),
        };
    }
}
