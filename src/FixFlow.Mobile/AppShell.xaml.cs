using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Features.ServiceEntries;
using FixFlow.Mobile.Features.Settings;
using FixFlow.Mobile.Features.Sync;
using FixFlow.Mobile.Features.WorkOrders;

namespace FixFlow.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(NavigationRoute.For<WorkOrderDetailsViewModel>(), typeof(WorkOrderDetailsPage));
        Routing.RegisterRoute(NavigationRoute.For<ServiceEntryViewModel>(), typeof(ServiceEntryPage));
        Routing.RegisterRoute(NavigationRoute.For<CorrectionEntryViewModel>(), typeof(CorrectionEntryPage));
        Routing.RegisterRoute(NavigationRoute.For<CompleteWorkOrderViewModel>(), typeof(CompleteWorkOrderPage));
        Routing.RegisterRoute(NavigationRoute.For<SyncViewModel>(), typeof(SyncPage));
        Routing.RegisterRoute(NavigationRoute.For<SettingsViewModel>(), typeof(SettingsPage));
    }

    protected override async void OnNavigating(ShellNavigatingEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        base.OnNavigating(args);
        if (args.Source != ShellNavigationSource.Pop || CurrentPage?.BindingContext is not ILeaveGuard guard)
        {
            return;
        }

        var deferral = args.GetDeferral();
        try
        {
            if (!await guard.CanLeaveAsync())
            {
                args.Cancel();
            }
        }
        finally
        {
            deferral.Complete();
        }
    }
}
