using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.WorkOrders;
using SQLite;

namespace FixFlow.Mobile.Core.Features.Sync;

public sealed class OutboxWriter(OutboxRepository outbox, TimeProvider timeProvider, IMessenger messenger)
{
    public Task<OutboxOperation> StartWorkAsync(Guid workOrderId)
    {
        var startedAt = timeProvider.GetUtcNow();
        var timer = WorkTimerRow.From(new WorkTimer(workOrderId, startedAt, WorkLocationStatus.Pending, null, null));
        return EnqueueAsync(
            workOrderId,
            OutboxOperationKind.StartWork,
            OutboxPayload.Serialize(new PendingStart(startedAt)),
            connection => connection.InsertOrReplace(timer));
    }

    public Task<OutboxOperation> AddServiceEntryAsync(Guid workOrderId, PendingServiceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return EnqueueAsync(workOrderId, OutboxOperationKind.AddServiceEntry, OutboxPayload.Serialize(entry));
    }

    public Task<OutboxOperation> CompleteWorkOrderAsync(Guid workOrderId) =>
        EnqueueAsync(workOrderId, OutboxOperationKind.CompleteWorkOrder, OutboxPayload.Empty);

    private async Task<OutboxOperation> EnqueueAsync(
        Guid workOrderId, OutboxOperationKind kind, string payload, Action<SQLiteConnection>? localChange = null)
    {
        var operation = new OutboxOperation(Guid.CreateVersion7(), workOrderId, kind, payload, timeProvider.GetUtcNow(), 0, null, null);
        await outbox.EnqueueAsync(operation, localChange);
        messenger.Send(new LocalChangeQueuedMessage(workOrderId));
        return operation;
    }
}
