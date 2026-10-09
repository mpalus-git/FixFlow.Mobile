using FixFlow.Mobile.Core.Features.Sync.Outbox;

namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public sealed class OrphanedPhotoCleaner(OutboxRepository outbox, PhotoStore photos)
{
    public async Task CleanAsync()
    {
        var queued = await outbox.GetAllAsync();
        var conflicts = await outbox.GetConflictsAsync();
        var referenced = queued
            .Concat(conflicts.Select(conflict => conflict.ToOperation()))
            .SelectMany(ReferencedPhotos)
            .ToHashSet();
        photos.DeleteAllExcept(referenced);
    }

    private static IEnumerable<Guid> ReferencedPhotos(OutboxOperation operation) => operation.Kind switch
    {
        OutboxOperationKind.UploadPhoto => [OutboxPayload.PhotoId(operation)],
        OutboxOperationKind.AddServiceEntry => OutboxPayload.ToServiceEntry(operation).Photos.Select(photo => photo.Id),
        _ => [],
    };
}
