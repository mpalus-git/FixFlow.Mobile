using FixFlow.Mobile.Core.Notifications;

namespace FixFlow.Mobile;

public sealed class DisabledNotificationScheduler : INotificationScheduler
{
    public bool IsSupported => false;

    public Task<bool> AreNotificationsEnabledAsync() => Task.FromResult(false);

    public Task<bool> RequestPermissionAsync() => Task.FromResult(false);

    public Task<IReadOnlyList<ScheduledReminder>> GetScheduledAsync() => Task.FromResult<IReadOnlyList<ScheduledReminder>>([]);

    public Task ScheduleAsync(ScheduledReminder reminder) => Task.CompletedTask;

    public Task CancelAsync(IReadOnlyCollection<int> notificationIds) => Task.CompletedTask;

    public Task CancelAllAsync() => Task.CompletedTask;
}
