using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed class WorkOrderListPresenter(WorkOrderSectionClassifier classifier, DisplayFormatter formatter)
{
    public IReadOnlyList<WorkOrderGroup> Present(IEnumerable<WorkOrder> workOrders, IReadOnlySet<Guid> pendingWorkOrderIds)
    {
        ArgumentNullException.ThrowIfNull(pendingWorkOrderIds);

        return
        [
            .. classifier.Group(workOrders).Select(group => new WorkOrderGroup(
                group.Section,
                WorkOrderTexts.Section(group.Section),
                group.WorkOrders.Select(workOrder => ToItem(group.Section, workOrder, pendingWorkOrderIds.Contains(workOrder.Id))))),
        ];
    }

    private WorkOrderListItem ToItem(WorkOrderSection section, WorkOrder workOrder, bool isPending) => new(
        workOrder.Id,
        workOrder.Number,
        workOrder.ClientName,
        workOrder.DeviceModel,
        section == WorkOrderSection.RecentlyCompleted && workOrder.CompletedAt is { } completedAt
            ? WorkOrderTexts.Format(AppResources.WorkOrderCompletedAt, formatter.FormatDateTime(completedAt))
            : WorkOrderTexts.Format(AppResources.WorkOrderDueAt, formatter.FormatDateTime(workOrder.DueDate)),
        workOrder.Status,
        WorkOrderTexts.Status(workOrder.Status),
        workOrder.Priority,
        WorkOrderTexts.Priority(workOrder.Priority),
        isPending);
}
