using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.Settings;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ILanguageService _language;
    private readonly ThemeService _theme;
    private readonly OutboxRepository _outbox;
    private readonly AuthService _authService;
    private readonly IDialogService _dialogs;

    public SettingsViewModel(
        ILanguageService language,
        ThemeService theme,
        OutboxRepository outbox,
        AuthService authService,
        SessionStore session,
        IAppVersion appVersion,
        IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(appVersion);
        _language = language;
        _theme = theme;
        _outbox = outbox;
        _authService = authService;
        _dialogs = dialogs;
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

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var pending = await _outbox.CountPendingAsync();
        var conflicts = await _outbox.CountConflictsAsync();
        if (await _dialogs.ConfirmAsync(
                AppResources.LogoutConfirmTitle,
                DescribeLoss(pending, conflicts),
                AppResources.LogoutButton,
                AppResources.CancelButton))
        {
            await _authService.LogoutAsync();
        }
    }

    private static string DescribeLoss(int pending, int conflicts)
    {
        string[] parts =
        [
            .. pending > 0 ? [WorkOrderTexts.Format(AppResources.LogoutPendingPart, pending)] : Array.Empty<string>(),
            .. conflicts > 0 ? [WorkOrderTexts.Format(AppResources.LogoutConflictPart, conflicts)] : Array.Empty<string>(),
        ];
        return parts.Length == 0
            ? AppResources.LogoutConfirmMessage
            : WorkOrderTexts.Format(AppResources.LogoutUnsentConfirmMessage, string.Join(", ", parts));
    }
}
