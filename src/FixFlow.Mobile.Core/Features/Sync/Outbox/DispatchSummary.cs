namespace FixFlow.Mobile.Core.Features.Sync.Outbox;

public sealed record DispatchSummary(int Sent, int Rejected, int Remaining, bool SessionRejected)
{
    public DateTimeOffset? NextAttemptAt { get; init; }

    public bool ChangedPartStock { get; init; }
}
