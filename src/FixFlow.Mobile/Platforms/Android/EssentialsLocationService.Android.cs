namespace FixFlow.Mobile.Location;

public sealed partial class EssentialsLocationService
{
    private static partial Task OpenPlatformSettingsAsync()
    {
        AppInfo.Current.ShowSettingsUI();
        return Task.CompletedTask;
    }
}
