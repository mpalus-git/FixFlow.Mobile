using FixFlow.Mobile.Core.Api;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed record WorkOrder(
    Guid Id,
    string Number,
    Guid DeviceId,
    string DeviceSerialNumber,
    string DeviceModel,
    Guid ClientId,
    string ClientName,
    string Description,
    WorkOrderPriority Priority,
    WorkOrderStatus Status,
    DateTimeOffset DueDate,
    bool IsOverdue,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? InvoicedAt)
{
    public ClientContact ClientContact { get; init; } = ClientContact.Empty;

    public Guid? ClientSignaturePhotoId { get; init; }
}
