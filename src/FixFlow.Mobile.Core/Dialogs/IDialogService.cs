namespace FixFlow.Mobile.Core.Dialogs;

public interface IDialogService
{
    Task ShowAlertAsync(string title, string message, string closeText);

    Task<bool> ConfirmAsync(string title, string message, string acceptText, string cancelText);
}
