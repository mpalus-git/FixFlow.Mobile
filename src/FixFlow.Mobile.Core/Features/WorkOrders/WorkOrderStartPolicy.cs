using FixFlow.Mobile.Core.Api;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public static class WorkOrderStartPolicy
{
    public static WorkOrderStartAvailability Evaluate(WorkOrder workOrder, IEnumerable<WorkOrder> technicianWorkOrders)
    {
        ArgumentNullException.ThrowIfNull(workOrder);
        ArgumentNullException.ThrowIfNull(technicianWorkOrders);

        if (workOrder.Status != WorkOrderStatus.Assigned)
        {
            return WorkOrderStartAvailability.NotAvailable;
        }

        return technicianWorkOrders.FirstOrDefault(other => other.Id != workOrder.Id && other.Status == WorkOrderStatus.InProgress) is { } inProgress
            ? new WorkOrderStartAvailability(false, inProgress.Number)
            : WorkOrderStartAvailability.Available;
    }
}
