using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.Auth;

public sealed partial class LoginViewModel(
    AuthService authService,
    IConnectivityService connectivity,
    INavigationService navigation) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

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

        var result = await authService.LoginAsync(Email.Trim(), Password, cancellationToken);
        switch (result.Outcome)
        {
            case LoginOutcome.Succeeded:
                Password = string.Empty;
                await navigation.NavigateToRootAsync<WorkOrderListViewModel>();
                break;
            case LoginOutcome.NotTechnician:
                Password = string.Empty;
                ErrorMessage = AppResources.NotTechnicianMessage;
                break;
            default:
                ErrorMessage = Describe(result.Error!);
                break;
        }
    }

    private static string Describe(ApiError error) => error switch
    {
        { Kind: ApiErrorKind.Unauthorized } => AppResources.LoginInvalidCredentials,
        { Kind: ApiErrorKind.Transient } => AppResources.LoginConnectionFailed,
        _ => error.Detail ?? AppResources.LoginFailed,
    };
}
