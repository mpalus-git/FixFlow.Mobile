namespace FixFlow.Mobile.Photos;

public sealed partial class EssentialsPhotoService
{
    private static partial Task OpenPlatformSettingsAsync() => Launcher.Default.OpenAsync(new Uri("ms-settings:privacy-webcam"));
}
