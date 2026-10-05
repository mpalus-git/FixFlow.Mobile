using FixFlow.Mobile.Core.Api;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed record DeviceHistoryItem(
    Guid WorkOrderId,
    Guid DeviceId,
    string Number,
    WorkOrderStatus Status,
    WorkOrderPriority Priority,
    string Description,
    string? TechnicianName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    int ServiceEntryCount);
