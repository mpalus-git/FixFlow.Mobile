using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using SQLite;

namespace FixFlow.Mobile.Core.Features.Sync;

public sealed class ConflictResolver(OutboxRepository outbox, PhotoStore photos, IMessenger messenger)
{
    public async Task RetryAsync(Guid operationId)
    {
        if (await outbox.RequeueConflictAsync(operationId) is { } conflict)
        {
            messenger.Send(new LocalChangeQueuedMessage(conflict.WorkOrderId));
        }
    }

    public async Task DiscardAsync(Guid operationId)
    {
        var conflict = await outbox.RemoveConflictAsync(operationId, (connection, removed) =>
        {
            if (removed.Kind == OutboxOperationKind.StartWork)
            {
                RemoveWorkTimer(connection, removed.WorkOrderId);
            }
            else if (removed.Kind == OutboxOperationKind.UploadPhoto)
            {
                DetachPhotoFromWaitingEntries(connection, removed);
                DetachSignatureFromWaitingCompletion(connection, removed);
            }
        });
        if (conflict is null)
        {
            return;
        }

        DeletePhotoFiles(conflict.ToOperation());
        messenger.Send(new LocalChangeQueuedMessage(conflict.WorkOrderId));
    }

    public async Task DiscardWorkOrderChangesAsync(Guid workOrderId)
    {
        var removed = await outbox.RemoveWorkOrderChangesAsync(workOrderId, (connection, operations) =>
        {
            if (operations.Any(operation => operation.Kind == OutboxOperationKind.StartWork))
            {
                RemoveWorkTimer(connection, workOrderId);
            }
        });
        foreach (var operation in removed)
        {
            DeletePhotoFiles(operation);
        }

        messenger.Send(new LocalChangeQueuedMessage(workOrderId));
    }

    private static void RemoveWorkTimer(SQLiteConnection connection, Guid workOrderId) =>
        connection.Execute("DELETE FROM work_timers WHERE work_order_id = ?", workOrderId);

    private static void DetachPhotoFromWaitingEntries(SQLiteConnection connection, SyncConflict upload)
    {
        var photoId = OutboxPayload.PhotoId(upload.ToOperation());
        var entries = connection
            .Query<OutboxOperationRow>(
                "SELECT * FROM outbox_operations WHERE work_order_id = ? AND kind = ?", upload.WorkOrderId, nameof(OutboxOperationKind.AddServiceEntry))
            .Select(row => row.ToOperation());
        foreach (var operation in entries)
        {
            var entry = OutboxPayload.ToServiceEntry(operation);
            if (entry.Photos.Any(photo => photo.Id == photoId))
            {
                var payload = OutboxPayload.Serialize(entry with { Photos = [.. entry.Photos.Where(photo => photo.Id != photoId)] });
                connection.Execute("UPDATE outbox_operations SET payload = ? WHERE id = ?", payload, operation.Id);
            }
        }
    }

    private static void DetachSignatureFromWaitingCompletion(SQLiteConnection connection, SyncConflict upload)
    {
        var photoId = OutboxPayload.PhotoId(upload.ToOperation());
        var completions = connection
            .Query<OutboxOperationRow>(
                "SELECT * FROM outbox_operations WHERE work_order_id = ? AND kind = ?", upload.WorkOrderId, nameof(OutboxOperationKind.CompleteWorkOrder))
            .Select(row => row.ToOperation())
            .Where(operation => OutboxPayload.ClientSignaturePhotoId(operation) == photoId);
        foreach (var operation in completions)
        {
            var payload = OutboxPayload.Serialize(new PendingCompletion(OutboxPayload.CompletedAt(operation)));
            connection.Execute("UPDATE outbox_operations SET payload = ? WHERE id = ?", payload, operation.Id);
        }
    }

    private void DeletePhotoFiles(OutboxOperation operation)
    {
        IEnumerable<Guid> photoIds = operation.Kind switch
        {
            OutboxOperationKind.UploadPhoto => [OutboxPayload.PhotoId(operation)],
            OutboxOperationKind.AddServiceEntry => OutboxPayload.ToServiceEntry(operation).Photos.Select(photo => photo.Id),
            _ => [],
        };
        foreach (var photoId in photoIds)
        {
            photos.Delete(photoId);
        }
    }
}
