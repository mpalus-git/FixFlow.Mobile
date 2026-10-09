using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Notifications;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed class DueDateReminderPlanner(BusinessClock clock, DisplayFormatter formatter)
{
    public static readonly TimeSpan LeadTime = TimeSpan.FromHours(1);

    public IReadOnlyList<ScheduledReminder> Plan(IEnumerable<WorkOrder> workOrders)
    {
        ArgumentNullException.ThrowIfNull(workOrders);

        var now = clock.Now;
        return
        [
            .. workOrders
                .Where(workOrder => WorkOrderTexts.IsActive(workOrder.Status) && workOrder.DueDate - LeadTime > now)
                .Select(workOrder => new ScheduledReminder(
                    NotificationIdFor(workOrder.Id),
                    workOrder.Id,
                    workOrder.DueDate - LeadTime,
                    AppResources.ReminderTitle,
                    AppText.Format(AppResources.ReminderMessage, workOrder.Number, workOrder.ClientName, formatter.FormatTime(workOrder.DueDate)))),
        ];
    }

    public static int NotificationIdFor(Guid workOrderId)
    {
        Span<byte> bytes = stackalloc byte[16];
        workOrderId.TryWriteBytes(bytes);
        var folded = BitConverter.ToInt32(bytes[..4]) ^ BitConverter.ToInt32(bytes[4..8]) ^ BitConverter.ToInt32(bytes[8..12]) ^ BitConverter.ToInt32(bytes[12..]);
        return folded & int.MaxValue;
    }
}
