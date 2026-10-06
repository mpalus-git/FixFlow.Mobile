namespace FixFlow.Mobile.Core.Notifications;

public interface INotificationScheduler
{
    Task<IReadOnlyList<ScheduledReminder>> GetScheduledAsync();

    Task ScheduleAsync(ScheduledReminder reminder);

    Task CancelAsync(IReadOnlyCollection<int> notificationIds);

    Task CancelAllAsync();
}
