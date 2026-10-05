using System.Globalization;
using System.Net;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    IUriLauncher uriLauncher) : ObservableObject
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
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; private set; }

    private bool CanLogin => !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrEmpty(Password);

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
