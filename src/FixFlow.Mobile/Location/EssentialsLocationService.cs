using FixFlow.Mobile.Core.Location;

namespace FixFlow.Mobile.Location;

public sealed partial class EssentialsLocationService(IGeolocation geolocation) : ILocationService
{
    public async Task<bool> HasPermissionAsync() =>
        await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>() == PermissionStatus.Granted;

    public async Task<bool> RequestPermissionAsync() =>
        await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.LocationWhenInUse>) == PermissionStatus.Granted;

    public async Task<GeoPosition?> GetCurrentPositionAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            var location = await geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, timeout), cancellationToken);
            return location is null ? null : new GeoPosition(location.Latitude, location.Longitude);
        }
        catch (Exception exception) when (exception is FeatureNotEnabledException or FeatureNotSupportedException or PermissionException)
        {
            return null;
        }
    }

    public Task OpenSettingsAsync() => OpenPlatformSettingsAsync();

    private static partial Task OpenPlatformSettingsAsync();
}
