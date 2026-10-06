namespace FixFlow.Mobile.Core.Notifications;

public interface INotificationScheduler
{
    bool IsSupported { get; }

    Task<bool> AreNotificationsEnabledAsync();

    Task<bool> RequestPermissionAsync();

    Task OpenSettingsAsync();

    Task<IReadOnlyList<ScheduledReminder>> GetScheduledAsync();

    Task ScheduleAsync(ScheduledReminder reminder);

    Task CancelAsync(IReadOnlyCollection<int> notificationIds);

    Task CancelAllAsync();
}
