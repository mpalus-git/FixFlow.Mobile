using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Formatting;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed class WorkOrderSectionClassifier(BusinessClock clock)
{
    public static readonly TimeSpan RecentlyCompletedPeriod = TimeSpan.FromDays(7);

    public WorkOrderSection? Classify(WorkOrder workOrder)
    {
        ArgumentNullException.ThrowIfNull(workOrder);

        switch (workOrder.Status)
        {
            case WorkOrderStatus.Assigned or WorkOrderStatus.InProgress:
                var dueDate = clock.ToBusinessDate(workOrder.DueDate);
                var today = clock.Today;
                if (workOrder.IsOverdue || dueDate < today)
                {
                    return WorkOrderSection.Overdue;
                }

                return dueDate == today ? WorkOrderSection.Today : WorkOrderSection.Upcoming;
            case WorkOrderStatus.Completed or WorkOrderStatus.Invoiced
                when workOrder.CompletedAt >= clock.Now - RecentlyCompletedPeriod:
                return WorkOrderSection.RecentlyCompleted;
            default:
                return null;
        }
    }

    public IReadOnlyList<WorkOrderSectionGroup> Group(IEnumerable<WorkOrder> workOrders)
    {
        ArgumentNullException.ThrowIfNull(workOrders);

        var bySection = workOrders
            .Select(workOrder => (Section: Classify(workOrder), WorkOrder: workOrder))
            .Where(entry => entry.Section is not null)
            .ToLookup(entry => entry.Section!.Value, entry => entry.WorkOrder);

        return
        [
            .. Enum.GetValues<WorkOrderSection>()
                .Where(section => bySection[section].Any())
                .Select(section => new WorkOrderSectionGroup(section, Order(section, bySection[section]))),
        ];
    }

    private static List<WorkOrder> Order(WorkOrderSection section, IEnumerable<WorkOrder> workOrders) =>
        section == WorkOrderSection.RecentlyCompleted
            ? [.. workOrders.OrderByDescending(workOrder => workOrder.CompletedAt)]
            : [.. workOrders.OrderBy(workOrder => workOrder.DueDate).ThenBy(workOrder => workOrder.Number, StringComparer.Ordinal)];
}
