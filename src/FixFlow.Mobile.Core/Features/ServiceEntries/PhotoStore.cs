using FixFlow.Mobile.Core.Storage;

namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public sealed class PhotoStore(LocalDatabaseOptions options, UserDatabase userDatabase)
{
    private string UserDirectory =>
        Path.Combine(options.Directory, "photos", (userDatabase.UserId ?? throw new InvalidOperationException("No user database is open.")).ToString("N"));

    public string PathFor(Guid photoId) => Path.Combine(UserDirectory, $"{photoId:N}.jpg");

    public async Task SaveAsync(Guid photoId, byte[] jpeg)
    {
        Directory.CreateDirectory(UserDirectory);
        await File.WriteAllBytesAsync(PathFor(photoId), jpeg);
    }

    public async Task<byte[]?> ReadAsync(Guid photoId)
    {
        var path = PathFor(photoId);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path) : null;
    }

    public string? ExistingPathFor(Guid photoId) => File.Exists(PathFor(photoId)) ? PathFor(photoId) : null;

    public void Delete(Guid photoId)
    {
        if (ExistingPathFor(photoId) is { } path)
        {
            File.Delete(path);
        }
    }
}
