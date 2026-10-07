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

    public void DeleteAll()
    {
        if (Directory.Exists(UserDirectory))
        {
            Directory.Delete(UserDirectory, recursive: true);
        }
    }

    public void DeleteAllExcept(IReadOnlySet<Guid> keptPhotoIds)
    {
        ArgumentNullException.ThrowIfNull(keptPhotoIds);
        if (!Directory.Exists(UserDirectory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(UserDirectory, "*.jpg"))
        {
            if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out var photoId) && !keptPhotoIds.Contains(photoId))
            {
                TryDelete(path);
            }
        }
    }

    public void Delete(Guid photoId)
    {
        if (ExistingPathFor(photoId) is { } path)
        {
            File.Delete(path);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }
    }
}
