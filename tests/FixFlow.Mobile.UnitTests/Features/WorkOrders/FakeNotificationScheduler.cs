using FixFlow.Mobile.Core.Notifications;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

internal sealed class FakeNotificationScheduler : INotificationScheduler
{
    private readonly Dictionary<int, ScheduledReminder> _scheduled = [];

    public bool IsSupported { get; init; } = true;

    public bool IsEnabled { get; set; } = true;

    public bool GrantsPermission { get; init; } = true;

    public int PermissionRequests { get; private set; }

    public int ScheduleCalls { get; private set; }

    public int SettingsOpenings { get; private set; }

    public IReadOnlyList<ScheduledReminder> Scheduled => [.. _scheduled.Values];

    public Task<bool> AreNotificationsEnabledAsync() => Task.FromResult(IsEnabled);

    public Task<bool> RequestPermissionAsync()
    {
        PermissionRequests++;
        IsEnabled = GrantsPermission;
        return Task.FromResult(GrantsPermission);
    }

    public Task OpenSettingsAsync()
    {
        SettingsOpenings++;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ScheduledReminder>> GetScheduledAsync() => Task.FromResult(Scheduled);

    public Task ScheduleAsync(ScheduledReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        ScheduleCalls++;
        _scheduled[reminder.NotificationId] = reminder;
        return Task.CompletedTask;
    }

    public Task CancelAsync(IReadOnlyCollection<int> notificationIds)
    {
        ArgumentNullException.ThrowIfNull(notificationIds);
        foreach (var id in notificationIds)
        {
            _scheduled.Remove(id);
        }

        return Task.CompletedTask;
    }

    public Task CancelAllAsync()
    {
        _scheduled.Clear();
        return Task.CompletedTask;
    }
}
