using System.Globalization;
using System.Net;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixFlow.Mobile.Core.Accessibility;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Launching;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.Auth;

public sealed partial class LoginViewModel(
    AuthService authService,
    ServerReadinessProbe readinessProbe,
    IConnectivityService connectivity,
    INavigationService navigation,
    IDialogService dialogs,
    IUriLauncher uriLauncher,
    DemoAccount demoAccount,
    IScreenReaderAnnouncer announcer) : ObservableObject
{
    public static readonly Uri WebPanelUri = new("https://fix-flow-web.vercel.app");

    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromMinutes(1);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    public partial string? StatusMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPasswordHidden), nameof(PasswordToggleText), nameof(PasswordToggleDescription))]
    public partial bool IsPasswordVisible { get; private set; }

    public bool IsPasswordHidden => !IsPasswordVisible;

    public string PasswordToggleText => IsPasswordVisible ? AppResources.HidePasswordButton : AppResources.ShowPasswordButton;

    public string PasswordToggleDescription => IsPasswordVisible ? AppResources.HidePasswordDescription : AppResources.ShowPasswordDescription;

    public bool HasErrorMessage => ErrorMessage is not null;

    public bool HasStatusMessage => StatusMessage is not null;

    partial void OnErrorMessageChanged(string? value) => AnnounceIfPresent(value);

    partial void OnStatusMessageChanged(string? value) => AnnounceIfPresent(value);

    public bool IsDemoLoginAvailable => demoAccount.IsAvailable;

    private bool CanLogin => !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrEmpty(Password);

    [RelayCommand]
    private void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        if (!connectivity.IsOnline)
        {
            ErrorMessage = AppResources.LoginOffline;
            return;
        }

        var isReady = await readinessProbe.WaitUntilReadyAsync(
            () => StatusMessage = AppResources.LoginServerStarting,
            cancellationToken);
        StatusMessage = null;
        if (!isReady)
        {
            ErrorMessage = AppResources.LoginServerUnavailable;
            return;
        }

        var result = await authService.LoginAsync(Email.Trim(), Password, cancellationToken);
        switch (result.Outcome)
        {
            case LoginOutcome.Succeeded:
                Password = string.Empty;
                await navigation.NavigateToRootAsync<WorkOrderListViewModel>();
                break;
            case LoginOutcome.NotTechnician:
                Password = string.Empty;
                await ShowWebPanelHintAsync();
                break;
            default:
                ErrorMessage = Describe(result.Error!);
                break;
        }
    }

    [RelayCommand]
    private Task DemoLoginAsync(CancellationToken cancellationToken)
    {
        Email = demoAccount.Email;
        Password = demoAccount.Password;
        return LoginAsync(cancellationToken);
    }

    private void AnnounceIfPresent(string? message)
    {
        if (message is not null)
        {
            announcer.Announce(message);
        }
    }

    private static string Describe(ApiError error) => error switch
    {
        { Kind: ApiErrorKind.Unauthorized } => AppResources.LoginInvalidCredentials,
        { StatusCode: HttpStatusCode.TooManyRequests } => string.Format(
            CultureInfo.CurrentCulture,
            CompositeFormat.Parse(AppResources.LoginRateLimited),
            (int)Math.Ceiling((error.RetryAfter ?? DefaultRetryAfter).TotalSeconds)),
        { Kind: ApiErrorKind.Transient } => AppResources.LoginConnectionFailed,
        { Kind: ApiErrorKind.Validation } => string.Join(Environment.NewLine, error.FieldErrors.Values.SelectMany(messages => messages)),
        _ => error.Detail ?? AppResources.LoginFailed,
    };

    private async Task ShowWebPanelHintAsync()
    {
        var openPanel = await dialogs.ConfirmAsync(
            AppResources.NotTechnicianTitle,
            AppResources.NotTechnicianMessage,
            AppResources.OpenWebPanelButton,
            AppResources.CloseButton);
        if (openPanel)
        {
            await uriLauncher.OpenAsync(WebPanelUri);
        }
    }
}
