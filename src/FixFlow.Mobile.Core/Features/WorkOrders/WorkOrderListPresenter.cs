using System.Globalization;
using System.Text;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed class WorkOrderListPresenter(WorkOrderSectionClassifier classifier, DisplayFormatter formatter)
{
    public IReadOnlyList<WorkOrderGroup> Present(IEnumerable<WorkOrder> workOrders) =>
    [
        .. classifier.Group(workOrders).Select(group => new WorkOrderGroup(
            group.Section,
            Text($"WorkOrderSection{group.Section}"),
            group.WorkOrders.Select(workOrder => ToItem(group.Section, workOrder)))),
    ];

    private WorkOrderListItem ToItem(WorkOrderSection section, WorkOrder workOrder) => new(
        workOrder.Id,
        workOrder.Number,
        workOrder.ClientName,
        workOrder.DeviceModel,
        section == WorkOrderSection.RecentlyCompleted && workOrder.CompletedAt is { } completedAt
            ? Format(AppResources.WorkOrderCompletedAt, formatter.FormatDateTime(completedAt))
            : Format(AppResources.WorkOrderDueAt, formatter.FormatDateTime(workOrder.DueDate)),
        workOrder.Status,
        Text($"WorkOrderStatus{workOrder.Status}"),
        workOrder.Priority,
        Text($"WorkOrderPriority{workOrder.Priority}"));

    private static string Text(string key) =>
        AppResources.ResourceManager.GetString(key, AppResources.Culture) ?? key;

    private static string Format(string pattern, string value) =>
        string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(pattern), value);
}
