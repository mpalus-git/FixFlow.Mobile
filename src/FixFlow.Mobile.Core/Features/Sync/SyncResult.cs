namespace FixFlow.Mobile.Core.Features.Sync;

public sealed record SyncResult(bool IsOffline, bool IsServerUnavailable, DispatchSummary? Dispatch, PullResult? Pull)
{
    public static SyncResult Offline { get; } = new(true, false, null, null);

    public static SyncResult ServerUnavailable { get; } = new(false, true, null, null);

    public static SyncResult NoSession { get; } = new(false, false, null, null);
}
