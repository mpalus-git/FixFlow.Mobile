namespace FixFlow.Mobile.Core.Features.Settings;

public sealed class ThemeService(ISettingsStore settingsStore)
{
    public const string ThemeKey = "theme";

    private const string LightCode = "light";
    private const string DarkCode = "dark";
    private const string SystemCode = "system";

    public AppThemePreference CurrentTheme { get; private set; } = AppThemePreference.System;

    public event EventHandler? ThemeChanged;

    public void ApplySavedTheme() =>
        CurrentTheme = settingsStore.GetString(ThemeKey) switch
        {
            LightCode => AppThemePreference.Light,
            DarkCode => AppThemePreference.Dark,
            _ => AppThemePreference.System,
        };

    public void ChangeTheme(AppThemePreference theme)
    {
        if (theme == CurrentTheme)
        {
            return;
        }

        CurrentTheme = theme;
        settingsStore.SetString(ThemeKey, theme switch
        {
            AppThemePreference.Light => LightCode,
            AppThemePreference.Dark => DarkCode,
            _ => SystemCode,
        });
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}
