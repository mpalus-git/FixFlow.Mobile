using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed record WorkOrderActions(
    bool CanStartWork,
    string? StartBlockedText,
    bool CanAddEntry,
    bool CanAddCorrection,
    bool CanComplete,
    string? CompleteBlockedText)
{
    public static WorkOrderActions None { get; } = new(false, null, false, false, false, null);

    public bool HasStartBlockedText => StartBlockedText is not null;

    public bool HasCompleteBlockedText => CompleteBlockedText is not null;

    public static WorkOrderActions Evaluate(
        WorkOrder workOrder,
        IEnumerable<WorkOrder> technicianWorkOrders,
        IReadOnlyCollection<ServiceEntry> storedEntries,
        IReadOnlyCollection<PendingServiceEntry> pendingEntries)
    {
        ArgumentNullException.ThrowIfNull(workOrder);
        ArgumentNullException.ThrowIfNull(storedEntries);
        ArgumentNullException.ThrowIfNull(pendingEntries);

        var start = WorkOrderStartPolicy.Evaluate(workOrder, technicianWorkOrders);
        var inProgress = workOrder.Status == WorkOrderStatus.InProgress;
        var hasEntries = storedEntries.Count > 0 || pendingEntries.Count > 0;
        return new WorkOrderActions(
            start.CanStart,
            start.BlockingWorkOrderNumber is { } number ? AppText.Format(AppResources.StartWorkBlocked, number) : null,
            inProgress,
            inProgress && NetPartUsage.Calculate(storedEntries, pendingEntries).Count > 0,
            inProgress && hasEntries,
            inProgress && !hasEntries ? AppResources.CompleteRequiresEntry : null);
    }
}
