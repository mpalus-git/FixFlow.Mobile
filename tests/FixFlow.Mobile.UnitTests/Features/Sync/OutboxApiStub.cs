using System.Net;
using System.Text;
using System.Text.Json;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

internal sealed class OutboxApiStub
{
    public List<string> Calls { get; } = [];

    public List<JsonDocument> SentEntries { get; } = [];

    public List<JsonDocument> SentStarts { get; } = [];

    public Dictionary<string, Func<HttpResponseMessage>> Overrides { get; } = [];

    public Dictionary<string, Func<Task<HttpResponseMessage>>> AsyncOverrides { get; } = [];

    public string RemoteStatus { get; set; } = "Assigned";

    public async Task<HttpResponseMessage> RouteAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var call = $"{request.Method} {request.RequestUri!.AbsolutePath}";
        lock (Calls)
        {
            Calls.Add(call);
        }
        JsonDocument? sentEntry = null;
        if (request.Content is not null && call.EndsWith("/service-entries", StringComparison.Ordinal))
        {
            sentEntry = JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
            SentEntries.Add(sentEntry);
        }

        if (request.Content is not null && call.EndsWith("/start", StringComparison.Ordinal))
        {
            SentStarts.Add(JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken)));
        }

        if (Overrides.TryGetValue(call, out var respond))
        {
            return respond();
        }

        if (AsyncOverrides.TryGetValue(call, out var respondAsync))
        {
            return await respondAsync();
        }

        if (sentEntry is not null)
        {
            var root = sentEntry.RootElement;
            var workOrderId = request.RequestUri.Segments[^2].TrimEnd('/');
            return Json(HttpStatusCode.Created, $$"""{"id":"{{root.GetProperty("id").GetGuid()}}","workOrderId":"{{workOrderId}}","technicianId":"{{Guid.Empty}}","technicianName":"Jan Kowalski","note":{{root.GetProperty("note").GetRawText()}},"isCorrection":false,"photoUrls":[],"parts":[],"createdAt":"2026-10-05T08:00:00Z"}""");
        }

        if (call is "GET /api/v1/work-orders" or "GET /api/v1/parts")
        {
            return Json(HttpStatusCode.OK, """{"items":[],"page":1,"pageSize":100,"totalCount":0}""");
        }

        if (request.Method == HttpMethod.Get)
        {
            return Json(HttpStatusCode.OK, $$"""{"id":"{{Guid.Empty}}","number":"ZL","deviceId":"{{Guid.Empty}}","deviceSerialNumber":"SN","deviceModel":"M","clientId":"{{Guid.Empty}}","clientName":"C","description":"D","priority":"Normal","status":"{{RemoteStatus}}","dueDate":"2026-10-05T08:00:00Z","isOverdue":false,"createdAt":"2026-10-01T08:00:00Z"}""");
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
    }

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, (int)statusCode < 300 ? "application/json" : "application/problem+json") };
}
