using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Notifications;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.Settings;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ILanguageService _language;
    private readonly ThemeService _theme;
    private readonly INotificationScheduler _notifications;
    private readonly ISettingsStore _settings;
    private readonly ReminderCoordinator _reminders;
    private readonly OutboxRepository _outbox;
    private readonly AuthService _authService;
    private readonly IDialogService _dialogs;

    public SettingsViewModel(
        ILanguageService language,
        ThemeService theme,
        INotificationScheduler notifications,
        ISettingsStore settings,
        ReminderCoordinator reminders,
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
        _notifications = notifications;
        _settings = settings;
        _reminders = reminders;
        _outbox = outbox;
        _authService = authService;
        _dialogs = dialogs;
        SelectedLanguage = language.CurrentLanguage;
        SelectedTheme = theme.CurrentTheme;
        UserName = session.CurrentUser?.FullName;
        UserEmail = session.CurrentUser?.Email;
        Version = AppText.Format(AppResources.SettingsVersion, appVersion.Version, appVersion.Build);
    }

    public string? UserName { get; }

    public string? UserEmail { get; }

    public string Version { get; }

    public bool AreNotificationsSupported => _notifications.IsSupported;

    [ObservableProperty]
    public partial AppLanguage SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial AppThemePreference SelectedTheme { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotificationStatus), nameof(CanEnableNotifications))]
    public partial bool AreNotificationsEnabled { get; private set; }

    public string NotificationStatus =>
        AreNotificationsEnabled ? AppResources.NotificationsEnabledStatus : AppResources.NotificationsDisabledStatus;

    public bool CanEnableNotifications => AreNotificationsSupported && !AreNotificationsEnabled;

    public string EnableNotificationsText =>
        WasPermissionRequested ? AppResources.OpenNotificationSettingsButton : AppResources.EnableNotificationsButton;

    private bool WasPermissionRequested => _settings.GetString(NotificationConsent.AskedKey) is not null;

    partial void OnSelectedLanguageChanged(AppLanguage value) => _language.ChangeLanguage(value);

    partial void OnSelectedThemeChanged(AppThemePreference value) => _theme.ChangeTheme(value);

    [RelayCommand]
    private async Task RefreshNotificationsAsync()
    {
        if (!AreNotificationsSupported)
        {
            return;
        }

        var wereEnabled = AreNotificationsEnabled;
        AreNotificationsEnabled = await _notifications.AreNotificationsEnabledAsync();
        OnPropertyChanged(nameof(EnableNotificationsText));
        if (AreNotificationsEnabled && !wereEnabled)
        {
            await _reminders.ReconcileAsync();
        }
    }

    [RelayCommand]
    private async Task EnableNotificationsAsync()
    {
        if (WasPermissionRequested)
        {
            await _notifications.OpenSettingsAsync();
            return;
        }

        _settings.SetString(NotificationConsent.AskedKey, bool.TrueString);
        await _notifications.RequestPermissionAsync();
        await RefreshNotificationsAsync();
    }

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
            .. pending > 0 ? [AppText.Format(AppResources.LogoutPendingPart, pending)] : Array.Empty<string>(),
            .. conflicts > 0 ? [AppText.Format(AppResources.LogoutConflictPart, conflicts)] : Array.Empty<string>(),
        ];
        return parts.Length == 0
            ? AppResources.LogoutConfirmMessage
            : AppText.Format(AppResources.LogoutUnsentConfirmMessage, string.Join(", ", parts));
    }
}
