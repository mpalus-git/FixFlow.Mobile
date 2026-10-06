using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Notifications;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.Core.Models.AndroidOption;

namespace FixFlow.Mobile;

public sealed class LocalNotificationScheduler(INotificationService notifications) : INotificationScheduler
{
    private const string SmallIconName = "notification_icon";

    public bool IsSupported => true;

    public Task<bool> AreNotificationsEnabledAsync() => notifications.AreNotificationsEnabled();

    public Task<bool> RequestPermissionAsync() => notifications.RequestNotificationPermission();

    public Task OpenSettingsAsync()
    {
        AppInfo.Current.ShowSettingsUI();
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ScheduledReminder>> GetScheduledAsync()
    {
        var pending = await notifications.GetPendingNotificationList();
        return
        [
            .. pending
                .Where(request => Guid.TryParse(request.ReturningData, out _) && request.Schedule.NotifyTime is not null)
                .Select(request => new ScheduledReminder(
                    request.NotificationId,
                    Guid.Parse(request.ReturningData),
                    request.Schedule.NotifyTime!.Value,
                    request.Title,
                    request.Description)),
        ];
    }

    public async Task ScheduleAsync(ScheduledReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        await notifications.Show(new NotificationRequest
        {
            NotificationId = reminder.NotificationId,
            Title = reminder.Title,
            Description = reminder.Message,
            ReturningData = reminder.WorkOrderId.ToString(),
            Android = { IconSmallName = new AndroidIcon(SmallIconName) },
            Schedule =
            {
                NotifyTime = reminder.NotifyAt,
                Android =
                {
                    ScheduleMode = AndroidScheduleMode.InexactAllowWhileIdle,
                    AllowedDelay = DueDateReminderPlanner.LeadTime,
                },
            },
        });
    }

    public Task CancelAsync(IReadOnlyCollection<int> notificationIds)
    {
        ArgumentNullException.ThrowIfNull(notificationIds);

        notifications.Cancel([.. notificationIds]);
        return Task.CompletedTask;
    }

    public Task CancelAllAsync()
    {
        notifications.CancelAll();
        return Task.CompletedTask;
    }
}
