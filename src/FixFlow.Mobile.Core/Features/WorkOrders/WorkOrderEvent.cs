using FixFlow.Mobile.Core.Api;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed record WorkOrderEvent(
    Guid Id,
    Guid WorkOrderId,
    WorkOrderEventType Type,
    DateTimeOffset OccurredAt,
    string? ActorName,
    string? TechnicianName,
    DateTimeOffset DueDate);
