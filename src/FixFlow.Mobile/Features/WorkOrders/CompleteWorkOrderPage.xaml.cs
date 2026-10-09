using FixFlow.Mobile.Core.Features.WorkOrders;

namespace FixFlow.Mobile.Features.WorkOrders;

public partial class CompleteWorkOrderPage : ContentPage
{
    public CompleteWorkOrderPage(CompleteWorkOrderViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
