using System.Net;
using System.Text;
using System.Text.Json;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

internal sealed class DetailsApiStub
{
    public static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    public Guid WorkOrderId { get; } = Guid.NewGuid();

    public Guid ClientId { get; } = Guid.NewGuid();

    public Guid DeviceId { get; } = Guid.NewGuid();

    public string Status { get; set; } = "InProgress";

    public Dictionary<string, HttpStatusCode> Failures { get; } = [];

    public HttpResponseMessage Route(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (Failures.TryGetValue(path, out var status))
        {
            return new HttpResponseMessage(status)
            {
                Content = new StringContent("""{"status":404,"errorCode":"WorkOrder.NotFound"}""", Encoding.UTF8, "application/problem+json"),
            };
        }

        if (path == $"/api/v1/work-orders/{WorkOrderId}/protocol")
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.7")) };
        }

        object content = path switch
        {
            _ when path == $"/api/v1/work-orders/{WorkOrderId}" => new
            {
                id = WorkOrderId, number = "ZL-2026-0042", deviceId = DeviceId, deviceSerialNumber = "SN-1", deviceModel = "Altherma 3",
                clientId = ClientId, clientName = "Piekarnia", clientContactPerson = "Anna Kowalska", clientPhone = "+48 600 100 200",
                clientAddress = new { street = "Długa", buildingNumber = "12A", postalCode = "31-147", city = "Kraków" }, description = "Przegląd", priority = "High", status = Status,
                dueDate = Now, isOverdue = false, createdAt = Now.AddDays(-2), startedAt = Now.AddHours(-1),
            },
            _ when path == $"/api/v1/devices/{DeviceId}" => new
            {
                id = DeviceId, clientId = ClientId, serialNumber = "SN-1", model = "Altherma 3", manufacturer = "Daikin",
                installationDate = "2021-03-14", createdAt = Now,
            },
            _ when path == $"/api/v1/work-orders/{WorkOrderId}/service-entries" => new[]
            {
                new
                {
                    id = Guid.NewGuid(), workOrderId = WorkOrderId, technicianId = Guid.NewGuid(), technicianName = "Jan Kowalski",
                    note = "Wymiana filtra", isCorrection = false, latitude = 50.0614, longitude = 19.9366, photoUrls = Array.Empty<string>(), createdAt = Now,
                    parts = new[] { new { partId = Guid.NewGuid(), partName = "Filtr", catalogNumber = "FP-100", quantity = 2, unitPrice = 12.34m } },
                },
            },
            _ when path == $"/api/v1/devices/{DeviceId}/work-orders" => new
            {
                page = 1, pageSize = 100, totalCount = 1,
                items = new[]
                {
                    new
                    {
                        id = Guid.NewGuid(), number = "ZL-2026-0007", status = "Invoiced", priority = "Normal", description = "Montaż",
                        technicianName = "Jan Kowalski", createdAt = Now.AddDays(-90), serviceEntryCount = 3,
                    },
                },
            },
            _ when path == $"/api/v1/work-orders/{WorkOrderId}/events" => new object[]
            {
                new { id = Guid.NewGuid(), type = "Created", occurredAt = Now.AddDays(-2), dueDate = Now },
                new { id = Guid.NewGuid(), type = "Assigned", occurredAt = Now.AddDays(-1), actorName = "Anna Nowak", technicianName = "Jan Kowalski", dueDate = Now },
            },
            _ => throw new InvalidOperationException($"Unexpected request {path}"),
        };

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json"),
        };
    }
}
