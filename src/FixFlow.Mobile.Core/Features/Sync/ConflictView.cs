namespace FixFlow.Mobile.Core.Features.Sync;

public sealed record ConflictView(
    Guid OperationId,
    Guid WorkOrderId,
    string Title,
    string Reason,
    string Details,
    string RejectedAtText,
    string Content,
    int FollowingChangeCount,
    string? FollowingChangesText)
{
    public bool HasDetails => Details.Length > 0;

    public bool HasFollowingChanges => FollowingChangeCount > 0;
}
