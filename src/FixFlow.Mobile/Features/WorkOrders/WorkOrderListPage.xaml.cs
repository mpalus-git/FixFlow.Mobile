using FixFlow.Mobile.Core.Features.WorkOrders;

namespace FixFlow.Mobile.Features.WorkOrders;

public partial class WorkOrderListPage : ContentPage
{
    private readonly WorkOrderListViewModel _viewModel;

    public WorkOrderListPage(WorkOrderListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}
