using FixFlow.Mobile.Core.Features.WorkOrders.Details;

namespace FixFlow.Mobile.Features.WorkOrders;

public partial class CompleteWorkOrderPage : ContentPage
{
    public CompleteWorkOrderPage(CompleteWorkOrderViewModel viewModel)
    {
        InitializeComponent();
        viewModel.Signature = Signature;
        BindingContext = viewModel;
    }
}
