using FixFlow.Mobile.Core.Photos;

namespace FixFlow.Mobile.Photos;

public sealed partial class EssentialsPhotoService(IMediaPicker mediaPicker) : IPhotoService
{
    public bool IsCaptureSupported => mediaPicker.IsCaptureSupported;

    public async Task<bool> HasCameraPermissionAsync() =>
        await Permissions.CheckStatusAsync<Permissions.Camera>() == PermissionStatus.Granted;

    public async Task<bool> RequestCameraPermissionAsync() =>
        await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.Camera>) == PermissionStatus.Granted;

    public async Task<byte[]?> CaptureAsync(CancellationToken cancellationToken)
    {
        var files = await PickFilesAsync(async () => await mediaPicker.CapturePhotoAsync(CreateOptions(1)) is { } file ? [file] : []);
        return files.Count == 0 ? null : await ReadAsync(files[0], cancellationToken);
    }

    public async Task<IReadOnlyList<byte[]>> PickAsync(int limit, CancellationToken cancellationToken)
    {
        var photos = new List<byte[]>();
        foreach (var file in (await PickFilesAsync(() => mediaPicker.PickPhotosAsync(CreateOptions(limit)))).Take(limit))
        {
            photos.Add(await ReadAsync(file, cancellationToken));
        }

        return photos;
    }

    public Task OpenSettingsAsync() => OpenPlatformSettingsAsync();

    private static partial Task OpenPlatformSettingsAsync();

    private static MediaPickerOptions CreateOptions(int limit) => new()
    {
        SelectionLimit = limit,
        MaximumWidth = PhotoCompression.MaxLongSide,
        MaximumHeight = PhotoCompression.MaxLongSide,
        CompressionQuality = PhotoCompression.JpegQuality,
        RotateImage = true,
        PreserveMetaData = false,
    };

    private static async Task<List<FileResult>> PickFilesAsync(Func<Task<List<FileResult>>> pick)
    {
        try
        {
            return await MainThread.InvokeOnMainThreadAsync(pick) ?? [];
        }
        catch (Exception exception) when (exception is FeatureNotSupportedException or PermissionException)
        {
            return [];
        }
    }

    private static async Task<byte[]> ReadAsync(FileResult file, CancellationToken cancellationToken)
    {
        await using var stream = await file.OpenReadAsync();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
