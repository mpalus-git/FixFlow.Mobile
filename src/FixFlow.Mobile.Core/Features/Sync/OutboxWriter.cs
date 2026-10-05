using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Features.ServiceEntries;

namespace FixFlow.Mobile.Core.Features.Sync;

public sealed class OutboxWriter(OutboxRepository outbox, TimeProvider timeProvider, IMessenger messenger)
{
    public Task<OutboxOperation> StartWorkAsync(Guid workOrderId) =>
        EnqueueAsync(workOrderId, OutboxOperationKind.StartWork, OutboxPayload.Empty);

    public Task<OutboxOperation> AddServiceEntryAsync(Guid workOrderId, PendingServiceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return EnqueueAsync(workOrderId, OutboxOperationKind.AddServiceEntry, OutboxPayload.Serialize(entry));
    }

    public Task<OutboxOperation> CompleteWorkOrderAsync(Guid workOrderId) =>
        EnqueueAsync(workOrderId, OutboxOperationKind.CompleteWorkOrder, OutboxPayload.Empty);

    private async Task<OutboxOperation> EnqueueAsync(Guid workOrderId, OutboxOperationKind kind, string payload)
    {
        var operation = new OutboxOperation(Guid.CreateVersion7(), workOrderId, kind, payload, timeProvider.GetUtcNow(), 0, null, null);
        await outbox.EnqueueAsync(operation);
        messenger.Send(new LocalChangeQueuedMessage(workOrderId));
        return operation;
    }
}
