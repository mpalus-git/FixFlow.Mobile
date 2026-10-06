using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Notifications;
using FixFlow.Mobile.Core.Storage;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed class ReminderReconciler(
    UserDatabase userDatabase,
    WorkOrderRepository workOrders,
    OutboxRepository outbox,
    DueDateReminderPlanner planner,
    INotificationScheduler scheduler)
{
    public async Task ReconcileAsync()
    {
        if (userDatabase.UserId is null)
        {
            return;
        }

        var desired = planner.Plan(PendingOperationsOverlay.Apply(await workOrders.GetAllAsync(), await outbox.GetAllAsync()));
        var scheduled = await scheduler.GetScheduledAsync();
        var stale = scheduled.Except(desired).Select(reminder => reminder.NotificationId).ToList();
        if (stale.Count > 0)
        {
            await scheduler.CancelAsync(stale);
        }

        foreach (var reminder in desired.Except(scheduled))
        {
            await scheduler.ScheduleAsync(reminder);
        }
    }
}
