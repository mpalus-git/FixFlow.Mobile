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

    public async Task<OutboxOperation> AddServiceEntryAsync(Guid workOrderId, PendingServiceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var queuedAt = timeProvider.GetUtcNow();
        var uploads = entry.Photos
            .Select((photo, index) => Create(workOrderId, OutboxOperationKind.UploadPhoto, OutboxPayload.Serialize(new PendingPhotoUpload(photo.Id)), queuedAt.AddTicks(index)))
            .ToList();
        var operation = Create(workOrderId, OutboxOperationKind.AddServiceEntry, OutboxPayload.Serialize(entry), queuedAt.AddTicks(uploads.Count));
        await outbox.EnqueueAsync(
            [.. uploads, operation],
            entry.WorkFinishedAt is { } finishedAt ? connection => RestartTimer(connection, workOrderId, finishedAt) : null);
        messenger.Send(new LocalChangeQueuedMessage(workOrderId));
        return operation;
    }

    public Task<OutboxOperation> CompleteWorkOrderAsync(Guid workOrderId) =>
        EnqueueAsync(workOrderId, OutboxOperationKind.CompleteWorkOrder, OutboxPayload.Empty);

    private static void RestartTimer(SQLiteConnection connection, Guid workOrderId, DateTimeOffset startedAt) =>
        connection.Execute(
            "UPDATE work_timers SET started_at = ?, location_status = ?, latitude = NULL, longitude = NULL WHERE work_order_id = ?",
            startedAt,
            nameof(WorkLocationStatus.NotRequired),
            workOrderId);

    private static OutboxOperation Create(Guid workOrderId, OutboxOperationKind kind, string payload, DateTimeOffset createdAt) =>
        new(Guid.CreateVersion7(), workOrderId, kind, payload, createdAt, 0, null, null);

    private async Task<OutboxOperation> EnqueueAsync(
        Guid workOrderId, OutboxOperationKind kind, string payload, Action<SQLiteConnection>? localChange = null)
    {
        var operation = Create(workOrderId, kind, payload, timeProvider.GetUtcNow());
        await outbox.EnqueueAsync(operation, localChange);
        messenger.Send(new LocalChangeQueuedMessage(workOrderId));
        return operation;
    }
}
