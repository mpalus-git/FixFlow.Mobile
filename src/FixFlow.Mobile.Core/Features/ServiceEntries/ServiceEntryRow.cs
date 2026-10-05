using SQLite;

namespace FixFlow.Mobile.Core.Features.ServiceEntries;

[Table("service_entries")]
internal sealed class ServiceEntryRow
{
    [PrimaryKey]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("work_order_id")]
    public Guid WorkOrderId { get; set; }

    [Column("technician_id")]
    public Guid TechnicianId { get; set; }

    [Column("technician_name")]
    public string TechnicianName { get; set; } = string.Empty;

    [Column("note")]
    public string Note { get; set; } = string.Empty;

    [Column("is_correction")]
    public bool IsCorrection { get; set; }

    [Column("work_started_at")]
    public DateTimeOffset? WorkStartedAt { get; set; }

    [Column("work_finished_at")]
    public DateTimeOffset? WorkFinishedAt { get; set; }

    [Column("latitude")]
    public double? Latitude { get; set; }

    [Column("longitude")]
    public double? Longitude { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    public static ServiceEntryRow From(ServiceEntry entry) => new()
    {
        Id = entry.Id,
        WorkOrderId = entry.WorkOrderId,
        TechnicianId = entry.TechnicianId,
        TechnicianName = entry.TechnicianName,
        Note = entry.Note,
        IsCorrection = entry.IsCorrection,
        WorkStartedAt = entry.WorkStartedAt,
        WorkFinishedAt = entry.WorkFinishedAt,
        Latitude = entry.Latitude,
        Longitude = entry.Longitude,
        CreatedAt = entry.CreatedAt,
    };

    public ServiceEntry ToServiceEntry(IReadOnlyList<ServiceEntryPart> parts) => new(
        Id, WorkOrderId, TechnicianId, TechnicianName, Note, IsCorrection, WorkStartedAt, WorkFinishedAt, Latitude, Longitude, CreatedAt, parts);
}
