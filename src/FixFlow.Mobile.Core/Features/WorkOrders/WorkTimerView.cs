namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed record WorkTimerView(
    string StartedText,
    string ElapsedText,
    string? LocationText,
    bool IsLocating,
    bool CanRetryLocation,
    bool CanOpenLocationSettings)
{
    public bool HasLocationText => LocationText is not null;
}
