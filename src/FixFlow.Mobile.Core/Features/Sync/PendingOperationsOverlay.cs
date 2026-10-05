using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.WorkOrders;

namespace FixFlow.Mobile.Core.Features.Sync;

public static class PendingOperationsOverlay
{
    public static IReadOnlyList<WorkOrder> Apply(IReadOnlyList<WorkOrder> workOrders, IReadOnlyList<OutboxOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(workOrders);
        ArgumentNullException.ThrowIfNull(operations);

        var byWorkOrder = operations.ToLookup(operation => operation.WorkOrderId);
        return [.. workOrders.Select(workOrder => Apply(workOrder, byWorkOrder[workOrder.Id]))];
    }

    public static WorkOrder Apply(WorkOrder workOrder, IEnumerable<OutboxOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(workOrder);
        ArgumentNullException.ThrowIfNull(operations);

        return operations.Where(operation => operation.WorkOrderId == workOrder.Id).Aggregate(workOrder, (current, operation) => operation.Kind switch
        {
            OutboxOperationKind.StartWork when current.Status == WorkOrderStatus.Assigned =>
                current with { Status = WorkOrderStatus.InProgress, StartedAt = current.StartedAt ?? OutboxPayload.StartedAt(operation) },
            OutboxOperationKind.CompleteWorkOrder when current.Status == WorkOrderStatus.InProgress =>
                current with { Status = WorkOrderStatus.Completed, CompletedAt = operation.CreatedAt, IsOverdue = false },
            _ => current,
        });
    }

    public static IReadOnlyList<PendingServiceEntry> PendingEntries(IEnumerable<OutboxOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        return [.. operations.Where(operation => operation.Kind == OutboxOperationKind.AddServiceEntry).Select(OutboxPayload.ToServiceEntry)];
    }
}
