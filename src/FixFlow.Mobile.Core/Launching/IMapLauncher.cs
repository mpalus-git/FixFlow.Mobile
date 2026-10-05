namespace FixFlow.Mobile.Core.Launching;

public interface IMapLauncher
{
    Task<bool> OpenAddressAsync(string name, string street, string postalCode, string city);

    Task<bool> OpenLocationAsync(string name, double latitude, double longitude);
}
