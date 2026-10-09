using SQLite;

namespace FixFlow.Mobile.Core.Features.Sync.Outbox;

[Table("outbox_operations")]
internal sealed class OutboxOperationRow
{
    [PrimaryKey]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("work_order_id")]
    public Guid WorkOrderId { get; set; }

    [Column("kind")]
    public string Kind { get; set; } = string.Empty;

    [Column("payload")]
    public string Payload { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("attempt_count")]
    public int AttemptCount { get; set; }

    [Column("next_attempt_at")]
    public DateTimeOffset? NextAttemptAt { get; set; }

    [Column("last_error")]
    public string? LastError { get; set; }

    public static OutboxOperationRow From(OutboxOperation operation) => new()
    {
        Id = operation.Id,
        WorkOrderId = operation.WorkOrderId,
        Kind = operation.Kind.ToString(),
        Payload = operation.Payload,
        CreatedAt = operation.CreatedAt,
        AttemptCount = operation.AttemptCount,
        NextAttemptAt = operation.NextAttemptAt,
        LastError = operation.LastError,
    };

    public OutboxOperation ToOperation() => new(
        Id, WorkOrderId, Enum.Parse<OutboxOperationKind>(Kind), Payload, CreatedAt, AttemptCount, NextAttemptAt, LastError);
}
