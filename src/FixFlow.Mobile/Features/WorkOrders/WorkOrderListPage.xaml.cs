using FixFlow.Mobile.Core.Features.WorkOrders;

namespace FixFlow.Mobile.Features.WorkOrders;

public partial class WorkOrderListPage : ContentPage
{
    public WorkOrderListPage(WorkOrderListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
