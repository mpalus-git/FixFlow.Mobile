using System.Net;
using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.UnitTests.Api;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;
using Refit;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

public sealed class OutboxDispatcherTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkOrderId = Guid.NewGuid();
    private static readonly Guid OtherWorkOrderId = Guid.NewGuid();

    private readonly TestUserDatabase _userDatabase = new();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly OutboxApiStub _api = new();
    private readonly StubHttpMessageHandler _handler;
    private readonly OutboxRepository _outbox;
    private readonly OutboxWriter _writer;
    private readonly OutboxDispatcher _dispatcher;

    public OutboxDispatcherTests()
    {
        _handler = new StubHttpMessageHandler(_api.RouteAsync);
        _outbox = new OutboxRepository(_userDatabase.Database);
        _writer = new OutboxWriter(_outbox, _timeProvider, new WeakReferenceMessenger());
        var client = RestService.ForGenerated<IFixFlowApi>(new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") });
        _dispatcher = new OutboxDispatcher(client, _outbox, new OutboxRetryPolicy(() => 0.5), _timeProvider);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Send_Work_Order_Operations_In_Creation_Order_With_Entry_Client_Id()
    {
        await QueueAsync(_writer.StartWorkAsync(WorkOrderId));
        var entry = await AddEntryAsync(WorkOrderId);
        await QueueAsync(_writer.CompleteWorkOrderAsync(WorkOrderId));

        var summary = await DispatchAsync();

        summary.ShouldBe(new DispatchSummary(Sent: 3, Rejected: 0, Remaining: 0, SessionRejected: false));
        _api.Calls.ShouldBe([$"POST /api/v1/work-orders/{WorkOrderId}/start", $"POST /api/v1/work-orders/{WorkOrderId}/service-entries", $"POST /api/v1/work-orders/{WorkOrderId}/complete"]);
        var sent = _api.SentEntries.ShouldHaveSingleItem().RootElement;
        sent.GetProperty("id").GetGuid().ShouldBe(entry.Id);
        sent.GetProperty("parts")[0].GetProperty("quantity").GetInt32().ShouldBe(2);
        (await _outbox.GetAllAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Send_Start_Time_From_Device_When_Start_Is_Sent_Later()
    {
        var startedOffline = _timeProvider.GetUtcNow();
        await QueueAsync(_writer.StartWorkAsync(WorkOrderId));
        _timeProvider.Advance(TimeSpan.FromHours(2));

        await DispatchAsync();

        _api.SentStarts.ShouldHaveSingleItem().RootElement.GetProperty("startedAt").GetDateTimeOffset().ShouldBe(startedOffline);
    }

    [Fact]
    public async Task Should_Keep_Operation_And_Hold_Later_Ones_When_Server_Is_Unavailable()
    {
        await QueueAsync(_writer.StartWorkAsync(WorkOrderId));
        await AddEntryAsync(WorkOrderId);
        _api.Overrides[$"POST /api/v1/work-orders/{WorkOrderId}/start"] = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var dispatchedAt = _timeProvider.GetUtcNow();

        var summary = await DispatchAsync();

        summary.Remaining.ShouldBe(2);
        _api.Calls.ShouldHaveSingleItem();
        var waiting = (await _outbox.GetAllAsync())[0];
        waiting.AttemptCount.ShouldBe(1);
        waiting.NextAttemptAt.ShouldBe(dispatchedAt.AddSeconds(5));
    }

    [Fact]
    public async Task Should_Send_Other_Work_Orders_When_One_Work_Order_Waits_For_Retry()
    {
        await QueueAsync(_writer.StartWorkAsync(WorkOrderId));
        await QueueAsync(_writer.CompleteWorkOrderAsync(OtherWorkOrderId));
        _api.Overrides[$"POST /api/v1/work-orders/{WorkOrderId}/start"] = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        var summary = await DispatchAsync();

        summary.Sent.ShouldBe(1);
        _api.Calls.ShouldContain($"POST /api/v1/work-orders/{OtherWorkOrderId}/complete");
    }

    [Fact]
    public async Task Should_Wait_For_Retry_After_When_Server_Limits_Requests()
    {
        await QueueAsync(_writer.StartWorkAsync(WorkOrderId));
        _api.Overrides[$"POST /api/v1/work-orders/{WorkOrderId}/start"] = () =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.Add("Retry-After", "120");
            return response;
        };

        var dispatchedAt = _timeProvider.GetUtcNow();
        await DispatchAsync();
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        await DispatchAsync();

        _api.Calls.Count.ShouldBe(1);
        (await _outbox.GetAllAsync())[0].NextAttemptAt.ShouldBe(dispatchedAt.AddMinutes(2));
    }

    [Fact]
    public async Task Should_Move_Operation_To_Conflicts_And_Block_Later_Ones_When_Api_Returns_404()
    {
        await AddEntryAsync(WorkOrderId);
        await QueueAsync(_writer.CompleteWorkOrderAsync(WorkOrderId));
        _api.Overrides[$"POST /api/v1/work-orders/{WorkOrderId}/service-entries"] = () => OutboxApiStub.Json(
            HttpStatusCode.NotFound, """{"status":404,"detail":"Work order was not found.","errorCode":"WorkOrder.NotFound"}""");

        var first = await DispatchAsync();
        _api.Overrides.Clear();
        var second = await DispatchAsync();

        first.ShouldBe(new DispatchSummary(Sent: 0, Rejected: 1, Remaining: 1, SessionRejected: false));
        second.Sent.ShouldBe(0);
        var conflict = (await _outbox.GetConflictsAsync()).ShouldHaveSingleItem();
        conflict.ErrorCode.ShouldBe("WorkOrder.NotFound");
        conflict.Kind.ShouldBe(OutboxOperationKind.AddServiceEntry);
        (await _outbox.GetAllAsync()).ShouldHaveSingleItem().Kind.ShouldBe(OutboxOperationKind.CompleteWorkOrder);
        _api.Calls.ShouldNotContain($"POST /api/v1/work-orders/{WorkOrderId}/complete");
    }

    [Fact]
    public async Task Should_Treat_Start_As_Sent_When_Api_Returns_409_And_Work_Order_Is_Already_In_Progress()
    {
        await QueueAsync(_writer.StartWorkAsync(WorkOrderId));
        _api.Overrides[$"POST /api/v1/work-orders/{WorkOrderId}/start"] = () => OutboxApiStub.Json(
            HttpStatusCode.Conflict, """{"status":409,"errorCode":"WorkOrder.InvalidStatusTransition"}""");
        _api.RemoteStatus = "InProgress";

        var summary = await DispatchAsync();

        summary.Sent.ShouldBe(1);
        (await _outbox.GetConflictsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Report_Conflict_When_Api_Returns_409_And_Work_Order_Is_Not_In_Target_Status()
    {
        await QueueAsync(_writer.StartWorkAsync(WorkOrderId));
        _api.Overrides[$"POST /api/v1/work-orders/{WorkOrderId}/start"] = () => OutboxApiStub.Json(
            HttpStatusCode.Conflict, """{"status":409,"errorCode":"WorkOrder.TechnicianHasWorkInProgress"}""");

        await DispatchAsync();

        (await _outbox.GetConflictsAsync()).ShouldHaveSingleItem().ErrorCode.ShouldBe("WorkOrder.TechnicianHasWorkInProgress");
    }

    [Fact]
    public async Task Should_Stop_And_Keep_Queue_When_Session_Is_Rejected()
    {
        await QueueAsync(_writer.StartWorkAsync(WorkOrderId));
        await QueueAsync(_writer.CompleteWorkOrderAsync(OtherWorkOrderId));
        _api.Overrides[$"POST /api/v1/work-orders/{WorkOrderId}/start"] = () => new HttpResponseMessage(HttpStatusCode.Unauthorized);

        var summary = await DispatchAsync();

        summary.SessionRejected.ShouldBeTrue();
        _api.Calls.ShouldHaveSingleItem();
        (await _outbox.GetAllAsync()).Count.ShouldBe(2);
        (await _outbox.GetAllAsync())[0].AttemptCount.ShouldBe(0);
    }

    public async ValueTask DisposeAsync()
    {
        _handler.Dispose();
        await _userDatabase.DisposeAsync();
    }

    private async Task QueueAsync(Task<OutboxOperation> enqueue)
    {
        await enqueue;
        _timeProvider.Advance(TimeSpan.FromMilliseconds(1));
    }

    private async Task<PendingServiceEntry> AddEntryAsync(Guid workOrderId)
    {
        var entry = new PendingServiceEntry(
            Guid.CreateVersion7(), "Wymiana filtra", false, Now.AddHours(-1), Now, null, null,
            [new PendingServiceEntryPart(Guid.NewGuid(), "Filtr", "FP-100", 2, 12.34m)]);
        await _writer.AddServiceEntryAsync(workOrderId, entry);
        _timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        return entry;
    }

    private Task<DispatchSummary> DispatchAsync() => _dispatcher.DispatchAsync(TestContext.Current.CancellationToken);
}
