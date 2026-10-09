namespace FixFlow.Mobile.Core.Features.Sync;

public sealed record DispatchSummary(int Sent, int Rejected, int Remaining, bool SessionRejected)
{
    public DateTimeOffset? NextAttemptAt { get; init; }
}
