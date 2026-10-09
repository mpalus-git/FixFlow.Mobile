namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed class WorkOrderGroup(WorkOrderSection section, string title, IEnumerable<WorkOrderListItem> items)
    : List<WorkOrderListItem>(items)
{
    public WorkOrderSection Section { get; } = section;

    public string Title { get; } = title;

    public override string ToString() => Title;

    public static bool HaveSameContent(IReadOnlyList<WorkOrderGroup> left, IReadOnlyList<WorkOrderGroup> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return left.Count == right.Count
            && left.Zip(right).All(pair => pair.First.Section == pair.Second.Section
                && pair.First.Title == pair.Second.Title
                && pair.First.SequenceEqual(pair.Second));
    }
}
