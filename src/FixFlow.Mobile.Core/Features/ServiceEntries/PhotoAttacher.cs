using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Photos;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public sealed class PhotoAttacher(IPhotoService photoService, PhotoStore store, ApiAddress apiAddress, IDialogService dialogs)
{
    public bool IsCaptureSupported => photoService.IsCaptureSupported;

    public async Task<PendingPhoto?> CaptureAsync(CancellationToken cancellationToken)
    {
        if (!await photoService.HasCameraPermissionAsync())
        {
            await dialogs.ShowAlertAsync(AppResources.CameraExplanationTitle, AppResources.CameraExplanationMessage, AppResources.ContinueButton);
            if (!await photoService.RequestCameraPermissionAsync())
            {
                if (await dialogs.ConfirmAsync(
                    AppResources.CameraExplanationTitle, AppResources.CameraDeniedMessage, AppResources.OpenSettingsButton, AppResources.CancelButton))
                {
                    await photoService.OpenSettingsAsync();
                }

                return null;
            }
        }

        return await StoreAsync(await photoService.CaptureAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<PendingPhoto>> PickAsync(int limit, CancellationToken cancellationToken)
    {
        var stored = new List<PendingPhoto>();
        foreach (var jpeg in await photoService.PickAsync(limit, cancellationToken))
        {
            if (await StoreAsync(jpeg) is { } photo)
            {
                stored.Add(photo);
            }
        }

        return stored;
    }

    public void Discard(PendingPhoto photo)
    {
        ArgumentNullException.ThrowIfNull(photo);
        store.Delete(photo.Id);
    }

    public string LocalPathFor(PendingPhoto photo)
    {
        ArgumentNullException.ThrowIfNull(photo);
        return store.PathFor(photo.Id);
    }

    private async Task<PendingPhoto?> StoreAsync(byte[]? jpeg)
    {
        if (jpeg is null)
        {
            return null;
        }

        var problem = !PhotoCompression.IsJpeg(jpeg) ? AppResources.PhotoUnsupportedFormat
            : jpeg.Length > PhotoCompression.MaxBytes ? AppResources.PhotoTooLarge
            : null;
        if (problem is not null)
        {
            await dialogs.ShowAlertAsync(AppResources.ErrorTitle, problem, AppResources.CloseButton);
            return null;
        }

        var photoId = Guid.CreateVersion7();
        await store.SaveAsync(photoId, jpeg);
        return new PendingPhoto(photoId, apiAddress.PhotoUrl(photoId));
    }
}
