namespace FixFlow.Mobile.Core.Notifications;

public sealed record ScheduledReminder(int NotificationId, Guid WorkOrderId, DateTimeOffset NotifyAt, string Title, string Message);
