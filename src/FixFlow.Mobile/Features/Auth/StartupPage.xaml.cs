using FixFlow.Mobile.Core.Features.Auth;

namespace FixFlow.Mobile.Features.Auth;

public partial class StartupPage : ContentPage
{
    private readonly StartupViewModel _viewModel;
    private bool _started;

    public StartupPage(StartupViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_started)
        {
            return;
        }

        _started = true;
        Dispatcher.Dispatch(async () => await _viewModel.StartAsync());
    }
}
