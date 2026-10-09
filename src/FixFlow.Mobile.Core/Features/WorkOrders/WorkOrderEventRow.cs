using FixFlow.Mobile.Core.Api;
using SQLite;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

[Table("work_order_events")]
internal sealed class WorkOrderEventRow
{
    [PrimaryKey]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("work_order_id")]
    public Guid WorkOrderId { get; set; }

    [Column("position")]
    public int Position { get; set; }

    [Column("type")]
    public string Type { get; set; } = string.Empty;

    [Column("occurred_at")]
    public DateTimeOffset OccurredAt { get; set; }

    [Column("actor_name")]
    public string? ActorName { get; set; }

    [Column("technician_name")]
    public string? TechnicianName { get; set; }

    [Column("due_date")]
    public DateTimeOffset DueDate { get; set; }

    public static WorkOrderEventRow From(WorkOrderEvent workOrderEvent, int position) => new()
    {
        Id = workOrderEvent.Id,
        WorkOrderId = workOrderEvent.WorkOrderId,
        Position = position,
        Type = workOrderEvent.Type.ToString(),
        OccurredAt = workOrderEvent.OccurredAt,
        ActorName = workOrderEvent.ActorName,
        TechnicianName = workOrderEvent.TechnicianName,
        DueDate = workOrderEvent.DueDate,
    };

    public WorkOrderEvent ToEvent() => new(Id, WorkOrderId, Enum.Parse<WorkOrderEventType>(Type), OccurredAt, ActorName, TechnicianName, DueDate);
}
