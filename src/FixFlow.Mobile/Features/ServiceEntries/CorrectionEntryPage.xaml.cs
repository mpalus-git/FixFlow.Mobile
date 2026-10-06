using FixFlow.Mobile.Core.Features.ServiceEntries;

namespace FixFlow.Mobile.Features.ServiceEntries;

public partial class CorrectionEntryPage : ContentPage
{
    public CorrectionEntryPage(CorrectionEntryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
