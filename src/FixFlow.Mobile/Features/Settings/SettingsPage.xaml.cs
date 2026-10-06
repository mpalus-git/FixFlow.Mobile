using FixFlow.Mobile.Core.Features.Settings;

namespace FixFlow.Mobile.Features.Settings;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;
    private Window? _window;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.RefreshNotificationsCommand.Execute(null);
        _window = Window;
        if (_window is not null)
        {
            _window.Resumed += OnWindowResumed;
        }
    }

    protected override void OnDisappearing()
    {
        if (_window is not null)
        {
            _window.Resumed -= OnWindowResumed;
            _window = null;
        }

        base.OnDisappearing();
    }

    private void OnWindowResumed(object? sender, EventArgs e) => _viewModel.RefreshNotificationsCommand.Execute(null);
}
