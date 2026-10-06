using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

public sealed class OutboxWriterTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkOrderId = Guid.NewGuid();

    private readonly TestUserDatabase _userDatabase = new();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly OutboxRepository _outbox;
    private readonly OutboxWriter _writer;

    public OutboxWriterTests()
    {
        _outbox = new OutboxRepository(_userDatabase.Database);
        _writer = new OutboxWriter(_outbox, _timeProvider, _messenger);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Store_Service_Entry_With_Client_Id_In_Queue_When_Entry_Is_Added()
    {
        var entry = new PendingServiceEntry(
            Guid.CreateVersion7(), "Wymiana filtra", false, Now.AddHours(-1), Now, 50.06, 19.94,
            [new PendingServiceEntryPart(Guid.NewGuid(), "Filtr", "FP-100", 2, 12.34m)]);

        var operation = await _writer.AddServiceEntryAsync(WorkOrderId, entry);

        var stored = (await _outbox.GetAllAsync()).ShouldHaveSingleItem();
        stored.ShouldBe(operation);
        stored.Kind.ShouldBe(OutboxOperationKind.AddServiceEntry);
        var payload = OutboxPayload.ToServiceEntry(stored);
        payload.Id.ShouldBe(entry.Id);
        payload.Parts.ShouldBe(entry.Parts);
    }

    [Fact]
    public async Task Should_Store_Start_And_Work_Timer_Together_When_Work_Is_Started()
    {
        var operation = await _writer.StartWorkAsync(WorkOrderId);

        (await _outbox.GetAllAsync()).ShouldHaveSingleItem().ShouldBe(operation);
        OutboxPayload.StartedAt(operation).ShouldBe(Now);
        var timer = (await new WorkTimerRepository(_userDatabase.Database).GetAsync(WorkOrderId)).ShouldNotBeNull();
        timer.ShouldBe(new WorkTimer(WorkOrderId, Now, WorkLocationStatus.Pending, null, null));
    }

    [Fact]
    public async Task Should_Restart_Timer_From_Entry_Finish_Without_Position_When_Work_Entry_Is_Queued()
    {
        var timers = new WorkTimerRepository(_userDatabase.Database);
        await _writer.StartWorkAsync(WorkOrderId);
        await timers.SetLocationAsync(WorkOrderId, WorkLocationStatus.Captured, 50.06, 19.94);
        var finishedAt = Now.AddMinutes(45);

        await _writer.AddServiceEntryAsync(WorkOrderId, new PendingServiceEntry(Guid.CreateVersion7(), "Przegląd", false, Now, finishedAt, 50.06, 19.94, []));

        (await timers.GetAsync(WorkOrderId)).ShouldBe(new WorkTimer(WorkOrderId, finishedAt, WorkLocationStatus.NotRequired, null, null));
    }

    [Fact]
    public async Task Should_Notify_About_Local_Change_When_Operation_Is_Queued()
    {
        LocalChangeQueuedMessage? received = null;
        _messenger.Register<LocalChangeQueuedMessage>(this, (_, message) => received = message);

        await _writer.StartWorkAsync(WorkOrderId);

        received.ShouldBe(new LocalChangeQueuedMessage(WorkOrderId));
        (await _outbox.GetAllAsync()).ShouldHaveSingleItem().CreatedAt.ShouldBe(Now);
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();
}
