namespace FixFlow.Mobile.Core.Photos;

public interface IPhotoService
{
    bool IsCaptureSupported { get; }

    Task<bool> HasCameraPermissionAsync();

    Task<bool> RequestCameraPermissionAsync();

    Task<byte[]?> CaptureAsync(CancellationToken cancellationToken);

    Task<byte[]?> PickAsync(CancellationToken cancellationToken);

    Task OpenSettingsAsync();
}
