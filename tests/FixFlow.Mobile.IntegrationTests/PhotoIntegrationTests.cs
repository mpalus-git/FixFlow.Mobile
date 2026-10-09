using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync.Outbox;

namespace FixFlow.Mobile.IntegrationTests;

public sealed class PhotoIntegrationTests(ApiEnvironment environment)
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0xFF, 0xD9];

    private static readonly byte[] SignatureJpeg = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAgAAAQABAAD//gAPTGF2YzYzLjEuMTAyAP/bAEMACAoKCwoLDQ0NDQ0NEA8QEBAQEBAQEBAQEBISEhUVFRISEhAQEhIUFBUVFxcXFRUVFRcXGRkZHh4cHCMjJCsrM//EAEsAAQEAAAAAAAAAAAAAAAAAAAAHAQEAAAAAAAAAAAAAAAAAAAAAEAEAAAAAAAAAAAAAAAAAAAAAEQEAAAAAAAAAAAAAAAAAAAAA/8AAEQgACAAQAwEiAAIRAAMRAP/aAAwDAQACEQMRAD8Av4AP/9k=");

    [Fact]
    public async Task Should_Upload_Photo_Before_Entry_And_Serve_It_Anonymously_When_Queue_Is_Sent()
    {
        using var admin = await AdminApi.LogInAsync(environment);
        var scenario = await TechnicianScenario.CreateAsync(admin);
        await using var device = await TechnicianDevice.LogInAsync(environment.BaseAddress, scenario.Email, scenario.Password);
        var photoId = Guid.CreateVersion7();
        await device.Get<PhotoStore>().SaveAsync(photoId, Jpeg);
        var photo = new PendingPhoto(photoId, device.Get<ApiAddress>().PhotoUrl(photoId));
        var writer = device.Get<OutboxWriter>();
        await writer.StartWorkAsync(scenario.WorkOrderId);
        var workStartedAt = DateTimeOffset.UtcNow;
        await writer.AddServiceEntryAsync(
            scenario.WorkOrderId,
            new PendingServiceEntry(Guid.CreateVersion7(), "Zdjęcie tabliczki", false, workStartedAt, workStartedAt.AddMinutes(1), null, null, []) { Photos = [photo] });

        var summary = await device.Get<OutboxDispatcher>().DispatchAsync(CancellationToken.None);

        summary.Sent.ShouldBe(3);
        var stored = (await device.Get<ServiceEntryRepository>().GetForWorkOrderAsync(scenario.WorkOrderId)).ShouldHaveSingleItem();
        stored.PhotoUrls.ShouldBe([photo.Url]);
        using var anonymous = new HttpClient();
        (await anonymous.GetByteArrayAsync(new Uri(photo.Url), TestContext.Current.CancellationToken)).ShouldBe(Jpeg);
        (await device.Get<PhotoUploadClient>().UploadAsync(photoId, Jpeg, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Attach_Client_Signature_When_Work_Order_Is_Completed_With_It()
    {
        using var admin = await AdminApi.LogInAsync(environment);
        var scenario = await TechnicianScenario.CreateAsync(admin);
        await using var device = await TechnicianDevice.LogInAsync(environment.BaseAddress, scenario.Email, scenario.Password);
        var writer = device.Get<OutboxWriter>();
        var start = await writer.StartWorkAsync(scenario.WorkOrderId);
        await writer.AddServiceEntryAsync(
            scenario.WorkOrderId,
            new PendingServiceEntry(Guid.CreateVersion7(), "Przegląd", false, OutboxPayload.StartedAt(start), DateTimeOffset.UtcNow, null, null, []));
        var signatureId = Guid.CreateVersion7();
        await device.Get<PhotoStore>().SaveAsync(signatureId, SignatureJpeg);
        await writer.CompleteWorkOrderAsync(scenario.WorkOrderId, signatureId);

        var summary = await device.Get<OutboxDispatcher>().DispatchAsync(CancellationToken.None);

        summary.ShouldBe(new DispatchSummary(Sent: 4, Rejected: 0, Remaining: 0, SessionRejected: false));
        (await admin.GetAsync($"api/v1/work-orders/{scenario.WorkOrderId}")).GetProperty("clientSignaturePhotoId").GetGuid().ShouldBe(signatureId);
    }
}
