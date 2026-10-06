namespace FixFlow.Mobile.Photos;

public sealed partial class EssentialsPhotoService
{
    private static partial Task OpenPlatformSettingsAsync()
    {
        AppInfo.Current.ShowSettingsUI();
        return Task.CompletedTask;
    }
}
