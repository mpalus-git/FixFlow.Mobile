using FixFlow.Mobile.Core.Api;

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
    string PriorityText)
{
    public bool IsInProgress => Status == WorkOrderStatus.InProgress;

    public bool IsUrgent => Priority is WorkOrderPriority.High or WorkOrderPriority.Critical;
}
