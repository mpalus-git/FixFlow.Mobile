using System.Net;

namespace FixFlow.Mobile.Core.Features.Sync;

public sealed record SyncConflict(
    Guid OperationId,
    Guid WorkOrderId,
    OutboxOperationKind Kind,
    string Payload,
    DateTimeOffset CreatedAt,
    HttpStatusCode? StatusCode,
    string? ErrorCode,
    string? Detail,
    DateTimeOffset RejectedAt)
{
    public OutboxOperation ToOperation() => new(OperationId, WorkOrderId, Kind, Payload, CreatedAt, 0, null, null);
}
