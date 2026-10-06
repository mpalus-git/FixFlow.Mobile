using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Storage;
using FixFlow.Mobile.UnitTests.Storage;

namespace FixFlow.Mobile.UnitTests.Features.ServiceEntries;

public sealed class PhotoStoreTests : IAsyncLifetime
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 1, 2];

    private readonly TemporaryDatabaseFile _directory = new();
    private readonly UserDatabase _userDatabase;
    private readonly PhotoStore _store;

    public PhotoStoreTests()
    {
        var options = new LocalDatabaseOptions(_directory.Directory);
        _userDatabase = new UserDatabase(options, TimeProvider.System);
        _store = new PhotoStore(options, _userDatabase);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Read_Saved_Photo_And_Forget_It_When_Deleted()
    {
        var photoId = Guid.NewGuid();

        await _store.SaveAsync(photoId, Jpeg);
        (await _store.ReadAsync(photoId)).ShouldBe(Jpeg);
        _store.Delete(photoId);

        (await _store.ReadAsync(photoId)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Keep_Photos_Of_Each_Technician_Apart_When_Other_User_Logs_In()
    {
        var photoId = Guid.NewGuid();
        await _store.SaveAsync(photoId, Jpeg);
        var firstPath = _store.PathFor(photoId);

        await _userDatabase.OpenAsync(Guid.NewGuid());

        _store.PathFor(photoId).ShouldNotBe(firstPath);
        (await _store.ReadAsync(photoId)).ShouldBeNull();
    }

    public async ValueTask DisposeAsync()
    {
        await _userDatabase.DisposeAsync();
        _directory.Dispose();
    }
}
