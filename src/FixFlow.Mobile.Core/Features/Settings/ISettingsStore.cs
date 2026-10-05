namespace FixFlow.Mobile.Core.Features.Settings;

public interface ISettingsStore
{
    string? GetString(string key);

    void SetString(string key, string value);
}
