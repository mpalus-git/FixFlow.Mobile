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
