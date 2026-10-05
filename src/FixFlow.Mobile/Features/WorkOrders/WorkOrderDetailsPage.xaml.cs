using FixFlow.Mobile.Core.Features.WorkOrders;

namespace FixFlow.Mobile.Features.WorkOrders;

public partial class WorkOrderDetailsPage : ContentPage
{
    public WorkOrderDetailsPage(WorkOrderDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
