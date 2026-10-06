namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed record WorkTimer(Guid WorkOrderId, DateTimeOffset StartedAt, WorkLocationStatus LocationStatus, double? Latitude, double? Longitude);
