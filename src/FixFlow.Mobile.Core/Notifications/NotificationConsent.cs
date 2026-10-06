using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Notifications;

public sealed class NotificationConsent(INotificationScheduler scheduler, ISettingsStore settings, IDialogService dialogs)
{
    public const string AskedKey = "notifications-asked";

    public async Task<bool> EnsureAllowedAsync()
    {
        if (await scheduler.AreNotificationsEnabledAsync())
        {
            return true;
        }

        if (settings.GetString(AskedKey) is not null)
        {
            return false;
        }

        settings.SetString(AskedKey, bool.TrueString);
        return await dialogs.ConfirmAsync(
                AppResources.NotificationExplanationTitle,
                AppResources.NotificationExplanationMessage,
                AppResources.ContinueButton,
                AppResources.NotNowButton)
            && await scheduler.RequestPermissionAsync();
    }
}
