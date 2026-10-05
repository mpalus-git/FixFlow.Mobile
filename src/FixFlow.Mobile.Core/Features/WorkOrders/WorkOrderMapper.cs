using FixFlow.Mobile.Core.Api;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public static class WorkOrderMapper
{
    public static WorkOrder FromListItem(WorkOrderListItemResponse item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new WorkOrder(
            item.Id,
            item.Number,
            item.DeviceId,
            item.DeviceSerialNumber,
            item.DeviceModel,
            item.ClientId,
            item.ClientName,
            item.Description,
            item.Priority,
            item.Status,
            item.DueDate,
            item.IsOverdue,
            item.CreatedAt,
            item.StartedAt,
            item.CompletedAt,
            item.InvoicedAt);
    }
}
