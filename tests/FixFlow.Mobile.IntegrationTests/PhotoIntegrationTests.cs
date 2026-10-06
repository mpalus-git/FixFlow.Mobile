using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync;

namespace FixFlow.Mobile.IntegrationTests;

public sealed class PhotoIntegrationTests(ApiEnvironment environment)
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0xFF, 0xD9];

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
}
