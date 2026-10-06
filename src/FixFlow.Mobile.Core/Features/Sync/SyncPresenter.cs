using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.Sync;

public sealed class SyncPresenter(LocalChangeDescriber describer, DisplayFormatter formatter)
{
    public IReadOnlyList<ConflictView> PresentConflicts(
        IReadOnlyList<SyncConflict> conflicts, IReadOnlyList<OutboxOperation> operations, IReadOnlyDictionary<Guid, string> workOrderNumbers)
    {
        ArgumentNullException.ThrowIfNull(conflicts);
        ArgumentNullException.ThrowIfNull(operations);

        return [.. conflicts.Select(conflict =>
        {
            var lines = describer.DescribeLines(conflict.ToOperation(), workOrderNumbers.GetValueOrDefault(conflict.WorkOrderId));
            var following = operations.Count(operation => operation.WorkOrderId == conflict.WorkOrderId);
            return new ConflictView(
                conflict.OperationId,
                conflict.WorkOrderId,
                lines[0],
                ConflictTexts.Reason(conflict),
                string.Join(Environment.NewLine, lines.Skip(1)),
                WorkOrderTexts.Format(AppResources.SyncRejectedAt, formatter.FormatDateTime(conflict.RejectedAt)),
                string.Join(Environment.NewLine, lines),
                following,
                following > 0 ? WorkOrderTexts.Format(AppResources.SyncFollowingChanges, following) : null);
        })];
    }

    public IReadOnlyList<PendingOperationView> PresentPending(
        IReadOnlyList<OutboxOperation> operations, IReadOnlyList<SyncConflict> conflicts, IReadOnlyDictionary<Guid, string> workOrderNumbers)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(conflicts);

        var heldWorkOrders = conflicts.Select(conflict => conflict.WorkOrderId).ToHashSet();
        return [.. operations.Select(operation =>
        {
            var lines = describer.DescribeLines(operation, workOrderNumbers.GetValueOrDefault(operation.WorkOrderId));
            var isHeld = heldWorkOrders.Contains(operation.WorkOrderId);
            return new PendingOperationView(
                operation.Id,
                lines[0],
                string.Join(Environment.NewLine, lines.Skip(1)),
                Status(operation, isHeld),
                isHeld);
        })];
    }

    private string Status(OutboxOperation operation, bool isHeld)
    {
        if (isHeld)
        {
            return AppResources.SyncPendingHeld;
        }

        return operation is { AttemptCount: > 0, NextAttemptAt: { } nextAttemptAt }
            ? WorkOrderTexts.Format(AppResources.SyncPendingRetry, operation.AttemptCount, formatter.FormatDateTime(nextAttemptAt))
            : AppResources.PendingSyncLabel;
    }
}
