namespace FixFlow.Mobile.Core.Features.Sync;

public sealed record PendingCompletion(DateTimeOffset? CompletedAt)
{
    public Guid? ClientSignaturePhotoId { get; init; }
}
