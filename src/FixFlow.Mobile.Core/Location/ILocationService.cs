namespace FixFlow.Mobile.Core.Location;

public interface ILocationService
{
    Task<bool> HasPermissionAsync();

    Task<bool> RequestPermissionAsync();

    Task<GeoPosition?> GetCurrentPositionAsync(TimeSpan timeout, CancellationToken cancellationToken);

    Task OpenSettingsAsync();
}
