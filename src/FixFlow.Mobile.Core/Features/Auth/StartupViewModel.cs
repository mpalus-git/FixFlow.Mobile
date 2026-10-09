using FixFlow.Mobile.Core.Features.WorkOrders.List;
using FixFlow.Mobile.Core.Navigation;

namespace FixFlow.Mobile.Core.Features.Auth;

public sealed class StartupViewModel(AuthService authService, INavigationService navigation)
{
    public async Task StartAsync()
    {
        if (await authService.RestoreSessionAsync() is not null)
        {
            await navigation.NavigateToRootAsync<WorkOrderListViewModel>();
        }
        else
        {
            await navigation.NavigateToRootAsync<LoginViewModel>();
        }
    }
}
