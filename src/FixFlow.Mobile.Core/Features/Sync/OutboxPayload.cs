using System.Text.Json;
using FixFlow.Mobile.Core.Features.ServiceEntries;

namespace FixFlow.Mobile.Core.Features.Sync;

public static class OutboxPayload
{
    public const string Empty = "{}";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(PendingServiceEntry entry) => JsonSerializer.Serialize(entry, Options);

    public static string Serialize(PendingStart start) => JsonSerializer.Serialize(start, Options);

    public static string Serialize(PendingPhotoUpload upload) => JsonSerializer.Serialize(upload, Options);

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

    public static PendingServiceEntry ToServiceEntry(OutboxOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return JsonSerializer.Deserialize<PendingServiceEntry>(operation.Payload, Options)
            ?? throw new InvalidOperationException($"Operation {operation.Id} has no service entry payload.");
    }
}
