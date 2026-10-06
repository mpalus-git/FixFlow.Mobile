using CommunityToolkit.Mvvm.ComponentModel;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.Settings;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ILanguageService _language;
    private readonly ThemeService _theme;

    public SettingsViewModel(
        ILanguageService language,
        ThemeService theme,
        SessionStore session,
        IAppVersion appVersion)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(appVersion);
        _language = language;
        _theme = theme;
        SelectedLanguage = language.CurrentLanguage;
        SelectedTheme = theme.CurrentTheme;
        UserName = session.CurrentUser?.FullName;
        UserEmail = session.CurrentUser?.Email;
        Version = WorkOrderTexts.Format(AppResources.SettingsVersion, appVersion.Version, appVersion.Build);
    }

    public string? UserName { get; }

    public string? UserEmail { get; }

    public string Version { get; }

    [ObservableProperty]
    public partial AppLanguage SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial AppThemePreference SelectedTheme { get; set; }

    partial void OnSelectedLanguageChanged(AppLanguage value) => _language.ChangeLanguage(value);

    partial void OnSelectedThemeChanged(AppThemePreference value) => _theme.ChangeTheme(value);
}
