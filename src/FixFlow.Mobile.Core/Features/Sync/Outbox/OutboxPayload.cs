using System.Text.Json;
using FixFlow.Mobile.Core.Features.ServiceEntries;

namespace FixFlow.Mobile.Core.Features.Sync.Outbox;

public static class OutboxPayload
{
    public const string Empty = "{}";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(PendingServiceEntry entry) => JsonSerializer.Serialize(entry, Options);

    public static string Serialize(PendingStart start) => JsonSerializer.Serialize(start, Options);

    public static string Serialize(PendingPhotoUpload upload) => JsonSerializer.Serialize(upload, Options);

    public static string Serialize(PendingCompletion completion) => JsonSerializer.Serialize(completion, Options);

    public static Guid PhotoId(OutboxOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return JsonSerializer.Deserialize<PendingPhotoUpload>(operation.Payload, Options)?.PhotoId
            ?? throw new InvalidOperationException($"Operation {operation.Id} has no photo payload.");
    }

    public static DateTimeOffset StartedAt(OutboxOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return JsonSerializer.Deserialize<PendingStart>(operation.Payload, Options)?.StartedAt ?? operation.CreatedAt;
    }

    public static DateTimeOffset CompletedAt(OutboxOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return JsonSerializer.Deserialize<PendingCompletion>(operation.Payload, Options)?.CompletedAt ?? operation.CreatedAt;
    }

    public static Guid? ClientSignaturePhotoId(OutboxOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return JsonSerializer.Deserialize<PendingCompletion>(operation.Payload, Options)?.ClientSignaturePhotoId;
    }

    public static PendingServiceEntry ToServiceEntry(OutboxOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return JsonSerializer.Deserialize<PendingServiceEntry>(operation.Payload, Options)
            ?? throw new InvalidOperationException($"Operation {operation.Id} has no service entry payload.");
    }
}
