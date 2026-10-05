using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Features.WorkOrders;

namespace FixFlow.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(NavigationRoute.For<WorkOrderDetailsViewModel>(), typeof(WorkOrderDetailsPage));
    }
}
