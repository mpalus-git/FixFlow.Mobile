using FixFlow.Mobile.Core.Launching;

namespace FixFlow.Mobile.Launching;

public sealed class EssentialsMapLauncher(IMap map) : IMapLauncher
{
    public Task<bool> OpenAddressAsync(string name, string street, string postalCode, string city) =>
        map.TryOpenAsync(
            new Placemark { Thoroughfare = street, PostalCode = postalCode, Locality = city, CountryName = "Polska" },
            new MapLaunchOptions { Name = name });

    public Task<bool> OpenLocationAsync(string name, double latitude, double longitude) =>
        map.TryOpenAsync(latitude, longitude, new MapLaunchOptions { Name = name });
}
