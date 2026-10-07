using System.Net;
using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.UnitTests.Storage;

namespace FixFlow.Mobile.UnitTests.Features.ServiceEntries;

public sealed class OrphanedPhotoCleanerTests : IAsyncLifetime
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 1, 2];

    private readonly TestUserDatabase _userDatabase = new();
    private readonly OutboxRepository _outbox;
    private readonly PhotoStore _photos;
    private readonly OrphanedPhotoCleaner _cleaner;

    public OrphanedPhotoCleanerTests()
    {
        _outbox = new OutboxRepository(_userDatabase.Database);
        _photos = new PhotoStore(_userDatabase.Options, _userDatabase.Database);
        _cleaner = new OrphanedPhotoCleaner(_outbox, _photos);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Delete_Photo_File_When_Nothing_References_It()
    {
        var abandoned = Guid.NewGuid();
        await _photos.SaveAsync(abandoned, Jpeg);

        await _cleaner.CleanAsync();

        _photos.ExistingPathFor(abandoned).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Keep_Photo_File_When_Queued_Operation_References_It()
    {
        var queued = Guid.NewGuid();
        var abandoned = Guid.NewGuid();
        await _photos.SaveAsync(queued, Jpeg);
        await _photos.SaveAsync(abandoned, Jpeg);
        await QueueEntryWithPhotoAsync(queued);

        await _cleaner.CleanAsync();

        _photos.ExistingPathFor(queued).ShouldNotBeNull();
        _photos.ExistingPathFor(abandoned).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Keep_Photo_File_When_Conflict_References_It()
    {
        var rejected = Guid.NewGuid();
        await _photos.SaveAsync(rejected, Jpeg);
        await QueueEntryWithPhotoAsync(rejected);
        foreach (var operation in await _outbox.GetAllAsync())
        {
            await _outbox.MoveToConflictsAsync(new SyncConflict(
                operation.Id, operation.WorkOrderId, operation.Kind, operation.Payload, operation.CreatedAt, HttpStatusCode.Conflict, "WorkOrder.NotInProgress", null, DateTimeOffset.UtcNow));
        }

        await _cleaner.CleanAsync();

        (await _outbox.GetAllAsync()).ShouldBeEmpty();
        _photos.ExistingPathFor(rejected).ShouldNotBeNull();
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();

    private Task<OutboxOperation> QueueEntryWithPhotoAsync(Guid photoId) =>
        new OutboxWriter(_outbox, TimeProvider.System, new WeakReferenceMessenger()).AddServiceEntryAsync(
            Guid.NewGuid(),
            new PendingServiceEntry(Guid.CreateVersion7(), "Ze zdjęciem", false, null, null, null, null, [])
            {
                Photos = [new PendingPhoto(photoId, $"https://api.test/api/v1/photos/{photoId}")],
            });
}
