using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.UnitTests.Api;
using FixFlow.Mobile.UnitTests.Features.WorkOrders;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;
using Refit;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

public sealed class WorkOrderSyncServiceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly TestUserDatabase _userDatabase = new();
    private readonly ServerWarmup _warmup = new();
    private readonly StubHttpMessageHandler _handler;
    private readonly WorkOrderRepository _workOrders;
    private readonly WorkOrderSyncService _service;
    private HttpStatusCode _readyStatus = HttpStatusCode.OK;
    private HttpStatusCode _listStatus = HttpStatusCode.OK;
    private int _remoteCount = 3;
    private int _startingNotices;

    public WorkOrderSyncServiceTests()
    {
        _handler = new StubHttpMessageHandler(Route);
        var httpClient = new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };
        _workOrders = new WorkOrderRepository(_userDatabase.Database);
        _service = new WorkOrderSyncService(
            RestService.ForGenerated<IFixFlowApi>(httpClient),
            new ServerReadinessProbe(httpClient, _timeProvider),
            _warmup,
            _workOrders,
            new SyncStateRepository(_userDatabase.Database, _timeProvider));
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Replace_Local_Work_Orders_With_All_Pages_When_Pull_Succeeds()
    {
        await _workOrders.ReplaceAllAsync([TestWorkOrders.Create("ZL-OLD", Now)]);
        _remoteCount = 150;

        var result = await PullAsync();

        result.ShouldBe(PullResult.Succeeded);
        var local = await _workOrders.GetAllAsync();
        local.Count.ShouldBe(150);
        local.ShouldNotContain(workOrder => workOrder.Number == "ZL-OLD");
        ListRequests().Select(query => query["page"]).ShouldBe(["1", "2"]);
        ListRequests().ShouldAllBe(query => query["pageSize"] == "100" && query["sortBy"] == "DueDate");
        (await _service.GetLastPulledAtAsync()).ShouldBe(Now);
    }

    [Fact]
    public async Task Should_Keep_Local_Work_Orders_When_Api_Fails()
    {
        var local = TestWorkOrders.Create("ZL-LOCAL", Now);
        await _workOrders.ReplaceAllAsync([local]);
        _listStatus = HttpStatusCode.ServiceUnavailable;

        var result = await PullAsync();

        result.Outcome.ShouldBe(PullOutcome.Failed);
        result.Error!.ShouldRetry.ShouldBeTrue();
        (await _workOrders.GetAllAsync()).ShouldBe([local]);
        (await _service.GetLastPulledAtAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Check_Server_Readiness_Only_Before_First_Pull()
    {
        await PullAsync();
        await PullAsync();

        _handler.Requests.Count(request => request.RequestUri!.AbsolutePath == "/api/v1/system/ready").ShouldBe(1);
    }

    [Fact]
    public async Task Should_Report_Server_Unavailable_Without_Pulling_When_Server_Does_Not_Start()
    {
        _readyStatus = HttpStatusCode.ServiceUnavailable;

        var pull = PullAsync();
        while (!pull.IsCompleted)
        {
            _timeProvider.Advance(TimeSpan.FromSeconds(3));
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        (await pull).ShouldBe(PullResult.ServerUnavailable);
        _startingNotices.ShouldBe(1);
        ListRequests().ShouldBeEmpty();
        _warmup.IsServerReady.ShouldBeFalse();
    }

    public async ValueTask DisposeAsync()
    {
        _handler.Dispose();
        await _userDatabase.DisposeAsync();
    }

    private Task<PullResult> PullAsync() => _service.PullAsync(() => _startingNotices++, TestContext.Current.CancellationToken);

    private List<System.Collections.Specialized.NameValueCollection> ListRequests() =>
    [
        .. _handler.Requests
            .Where(request => request.RequestUri!.AbsolutePath == "/api/v1/work-orders")
            .Select(request => HttpUtility.ParseQueryString(request.RequestUri!.Query)),
    ];

    private HttpResponseMessage Route(HttpRequestMessage request)
    {
        if (request.RequestUri!.AbsolutePath == "/api/v1/system/ready")
        {
            return new HttpResponseMessage(_readyStatus);
        }

        if (_listStatus != HttpStatusCode.OK)
        {
            return new HttpResponseMessage(_listStatus);
        }

        var page = int.Parse(HttpUtility.ParseQueryString(request.RequestUri.Query)["page"]!, System.Globalization.CultureInfo.InvariantCulture);
        var items = Enumerable.Range((page - 1) * WorkOrderSyncService.PageSize, WorkOrderSyncService.PageSize)
            .Where(index => index < _remoteCount)
            .Select(index => new
            {
                id = Guid.NewGuid(),
                number = $"ZL-2026-{index:D4}",
                deviceId = Guid.NewGuid(),
                deviceSerialNumber = "SN-1",
                deviceModel = "Daikin",
                clientId = Guid.NewGuid(),
                clientName = "Piekarnia",
                description = "Przegląd",
                priority = "High",
                status = "Assigned",
                dueDate = Now.AddDays(index),
                isOverdue = false,
                createdAt = Now.AddDays(-1),
            });
        var json = JsonSerializer.Serialize(new { items, page, pageSize = WorkOrderSyncService.PageSize, totalCount = _remoteCount });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
