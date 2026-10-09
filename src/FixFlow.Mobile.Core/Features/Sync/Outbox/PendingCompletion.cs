namespace FixFlow.Mobile.Core.Features.Sync.Outbox;

public sealed record PendingCompletion(DateTimeOffset? CompletedAt)
{
    public Guid? ClientSignaturePhotoId { get; init; }
}
