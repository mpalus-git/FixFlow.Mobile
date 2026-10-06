using SQLite;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

[Table("work_timers")]
internal sealed class WorkTimerRow
{
    [PrimaryKey]
    [Column("work_order_id")]
    public Guid WorkOrderId { get; set; }

    [Column("started_at")]
    public DateTimeOffset StartedAt { get; set; }

    [Column("location_status")]
    public string LocationStatus { get; set; } = string.Empty;

    [Column("latitude")]
    public double? Latitude { get; set; }

    [Column("longitude")]
    public double? Longitude { get; set; }

    public static WorkTimerRow From(WorkTimer timer) => new()
    {
        WorkOrderId = timer.WorkOrderId,
        StartedAt = timer.StartedAt,
        LocationStatus = timer.LocationStatus.ToString(),
        Latitude = timer.Latitude,
        Longitude = timer.Longitude,
    };

    public WorkTimer ToWorkTimer() => new(WorkOrderId, StartedAt, Enum.Parse<WorkLocationStatus>(LocationStatus), Latitude, Longitude);
}
