using System.Net;
using SQLite;

namespace FixFlow.Mobile.Core.Features.Sync;

[Table("sync_conflicts")]
internal sealed class SyncConflictRow
{
    [PrimaryKey]
    [Column("operation_id")]
    public Guid OperationId { get; set; }

    [Column("work_order_id")]
    public Guid WorkOrderId { get; set; }

    [Column("kind")]
    public string Kind { get; set; } = string.Empty;

    [Column("payload")]
    public string Payload { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("status_code")]
    public int? StatusCode { get; set; }

    [Column("error_code")]
    public string? ErrorCode { get; set; }

    [Column("detail")]
    public string? Detail { get; set; }

    [Column("rejected_at")]
    public DateTimeOffset RejectedAt { get; set; }

    public static SyncConflictRow From(SyncConflict conflict) => new()
    {
        OperationId = conflict.OperationId,
        WorkOrderId = conflict.WorkOrderId,
        Kind = conflict.Kind.ToString(),
        Payload = conflict.Payload,
        CreatedAt = conflict.CreatedAt,
        StatusCode = (int?)conflict.StatusCode,
        ErrorCode = conflict.ErrorCode,
        Detail = conflict.Detail,
        RejectedAt = conflict.RejectedAt,
    };

    public SyncConflict ToConflict() => new(
        OperationId,
        WorkOrderId,
        Enum.Parse<OutboxOperationKind>(Kind),
        Payload,
        CreatedAt,
        (HttpStatusCode?)StatusCode,
        ErrorCode,
        Detail,
        RejectedAt);
}
