using FixFlow.Mobile.Core.Api;
using SQLite;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

[Table("work_orders")]
internal sealed class WorkOrderRow
{
    [PrimaryKey]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("number")]
    public string Number { get; set; } = string.Empty;

    [Column("device_id")]
    public Guid DeviceId { get; set; }

    [Column("device_serial_number")]
    public string DeviceSerialNumber { get; set; } = string.Empty;

    [Column("device_model")]
    public string DeviceModel { get; set; } = string.Empty;

    [Column("client_id")]
    public Guid ClientId { get; set; }

    [Column("client_name")]
    public string ClientName { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("priority")]
    public string Priority { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = string.Empty;

    [Column("due_date")]
    public DateTimeOffset DueDate { get; set; }

    [Column("is_overdue")]
    public bool IsOverdue { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("started_at")]
    public DateTimeOffset? StartedAt { get; set; }

    [Column("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }

    [Column("invoiced_at")]
    public DateTimeOffset? InvoicedAt { get; set; }

    public static WorkOrderRow From(WorkOrder workOrder) => new()
    {
        Id = workOrder.Id,
        Number = workOrder.Number,
        DeviceId = workOrder.DeviceId,
        DeviceSerialNumber = workOrder.DeviceSerialNumber,
        DeviceModel = workOrder.DeviceModel,
        ClientId = workOrder.ClientId,
        ClientName = workOrder.ClientName,
        Description = workOrder.Description,
        Priority = workOrder.Priority.ToString(),
        Status = workOrder.Status.ToString(),
        DueDate = workOrder.DueDate,
        IsOverdue = workOrder.IsOverdue,
        CreatedAt = workOrder.CreatedAt,
        StartedAt = workOrder.StartedAt,
        CompletedAt = workOrder.CompletedAt,
        InvoicedAt = workOrder.InvoicedAt,
    };

    public WorkOrder ToWorkOrder() => new(
        Id,
        Number,
        DeviceId,
        DeviceSerialNumber,
        DeviceModel,
        ClientId,
        ClientName,
        Description,
        Enum.Parse<WorkOrderPriority>(Priority),
        Enum.Parse<WorkOrderStatus>(Status),
        DueDate,
        IsOverdue,
        CreatedAt,
        StartedAt,
        CompletedAt,
        InvoicedAt);
}
