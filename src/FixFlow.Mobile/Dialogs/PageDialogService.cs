using FixFlow.Mobile.Core.Dialogs;

namespace FixFlow.Mobile.Dialogs;

public sealed class PageDialogService : IDialogService
{
    private static Page CurrentPage => Shell.Current.CurrentPage;

    public Task ShowAlertAsync(string title, string message, string closeText) =>
        CurrentPage.DisplayAlertAsync(title, message, closeText);

    public Task<bool> ConfirmAsync(string title, string message, string acceptText, string cancelText) =>
        CurrentPage.DisplayAlertAsync(title, message, acceptText, cancelText);
}
