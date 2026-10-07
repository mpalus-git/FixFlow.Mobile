namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed class WorkOrderGroup(WorkOrderSection section, string title, IEnumerable<WorkOrderListItem> items)
    : List<WorkOrderListItem>(items)
{
    public WorkOrderSection Section { get; } = section;

    public string Title { get; } = title;

    public override string ToString() => Title;
}
