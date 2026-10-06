using FixFlow.Mobile.Core.Features.WorkOrders;

namespace FixFlow.Mobile.Features.WorkOrders;

public partial class WorkOrderDetailsPage : ContentPage
{
    private readonly WorkOrderDetailsViewModel _viewModel;

    public WorkOrderDetailsPage(WorkOrderDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Resume();
    }

    protected override void OnDisappearing()
    {
        _viewModel.Pause();
        base.OnDisappearing();
    }
}
