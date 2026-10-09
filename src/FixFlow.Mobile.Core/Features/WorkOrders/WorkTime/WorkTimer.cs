namespace FixFlow.Mobile.Core.Features.WorkOrders.WorkTime;

public sealed record WorkTimer(Guid WorkOrderId, DateTimeOffset StartedAt, WorkLocationStatus LocationStatus, double? Latitude, double? Longitude);
