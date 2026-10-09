using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed record WorkOrderListItem(
    Guid Id,
    string Number,
    string ClientName,
    string DeviceModel,
    string DateText,
    WorkOrderStatus Status,
    string StatusText,
    WorkOrderPriority Priority,
    string PriorityText,
    bool IsPending,
    bool HasRejectedChange)
{
    public BadgeTone StatusTone => WorkOrderBadges.ForStatus(Status);

    public BadgeTone PriorityTone => WorkOrderBadges.ForPriority(Priority);

    public bool IsUrgent =>
        Priority is WorkOrderPriority.High or WorkOrderPriority.Critical && WorkOrderTexts.IsActive(Status);

    public override string ToString() => string.Join(", ", ScreenReaderParts());

    private IEnumerable<string> ScreenReaderParts()
    {
        yield return Number;
        yield return StatusText;
        yield return ClientName;
        yield return DeviceModel;
        yield return DateText;
        if (IsUrgent)
        {
            yield return PriorityText;
        }

        if (IsPending)
        {
            yield return AppResources.PendingSyncLabel;
        }

        if (HasRejectedChange)
        {
            yield return AppResources.WorkOrderRejectedChange;
        }
    }
}
