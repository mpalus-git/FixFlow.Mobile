using FixFlow.Mobile.Core.Features.Settings;

namespace FixFlow.Mobile.Features.Settings;

public sealed class PreferencesSettingsStore(IPreferences preferences) : ISettingsStore
{
    public string? GetString(string key) => preferences.Get<string?>(key, null);

    public void SetString(string key, string value) => preferences.Set(key, value);
}
