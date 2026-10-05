using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.Auth;

public sealed class SessionEndNavigator(SessionStore session, INavigationService navigation, IDialogService dialogs)
{
    public void Start() => session.SessionEnded += OnSessionEnded;

    private async void OnSessionEnded(object? sender, SessionEndedEventArgs e) => await ShowLoginAsync(e.Reason);

    private async Task ShowLoginAsync(SessionEndReason reason)
    {
        await navigation.NavigateToRootAsync<LoginViewModel>();
        if (reason == SessionEndReason.Expired)
        {
            await dialogs.ShowAlertAsync(
                AppResources.SessionExpiredTitle,
                AppResources.SessionExpiredMessage,
                AppResources.CloseButton);
        }
    }
}
