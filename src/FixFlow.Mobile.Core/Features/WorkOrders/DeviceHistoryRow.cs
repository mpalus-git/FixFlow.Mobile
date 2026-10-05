using FixFlow.Mobile.Core.Api;
using SQLite;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

[Table("device_work_orders")]
internal sealed class DeviceHistoryRow
{
    [Column("device_id")]
    public Guid DeviceId { get; set; }

    [Column("work_order_id")]
    public Guid WorkOrderId { get; set; }

    [Column("number")]
    public string Number { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = string.Empty;

    [Column("priority")]
    public string Priority { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("technician_name")]
    public string? TechnicianName { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("started_at")]
    public DateTimeOffset? StartedAt { get; set; }

    [Column("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }

    [Column("service_entry_count")]
    public int ServiceEntryCount { get; set; }

    public static DeviceHistoryRow From(DeviceHistoryItem item) => new()
    {
        DeviceId = item.DeviceId,
        WorkOrderId = item.WorkOrderId,
        Number = item.Number,
        Status = item.Status.ToString(),
        Priority = item.Priority.ToString(),
        Description = item.Description,
        TechnicianName = item.TechnicianName,
        CreatedAt = item.CreatedAt,
        StartedAt = item.StartedAt,
        CompletedAt = item.CompletedAt,
        ServiceEntryCount = item.ServiceEntryCount,
    };

    public DeviceHistoryItem ToItem() => new(
        WorkOrderId,
        DeviceId,
        Number,
        Enum.Parse<WorkOrderStatus>(Status),
        Enum.Parse<WorkOrderPriority>(Priority),
        Description,
        TechnicianName,
        CreatedAt,
        StartedAt,
        CompletedAt,
        ServiceEntryCount);
}
