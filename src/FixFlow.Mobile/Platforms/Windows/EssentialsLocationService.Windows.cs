namespace FixFlow.Mobile.Location;

public sealed partial class EssentialsLocationService
{
    private static partial Task OpenPlatformSettingsAsync() => Launcher.Default.OpenAsync(new Uri("ms-settings:privacy-location"));
}
