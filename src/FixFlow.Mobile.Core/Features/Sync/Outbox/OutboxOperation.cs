namespace FixFlow.Mobile.Core.Features.Sync.Outbox;

public sealed record OutboxOperation(
    Guid Id,
    Guid WorkOrderId,
    OutboxOperationKind Kind,
    string Payload,
    DateTimeOffset CreatedAt,
    int AttemptCount,
    DateTimeOffset? NextAttemptAt,
    string? LastError);
