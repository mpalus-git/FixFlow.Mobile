using FixFlow.Mobile.Core.Features.ServiceEntries;

namespace FixFlow.Mobile.Features.ServiceEntries;

public partial class ServiceEntryPage : ContentPage
{
    public ServiceEntryPage(ServiceEntryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
