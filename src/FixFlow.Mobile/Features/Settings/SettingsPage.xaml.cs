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
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.RefreshNotificationsCommand.Execute(null);
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _window = Window;
        if (_window is not null)
        {
            _window.Resumed += OnWindowResumed;
        }
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_window is not null)
        {
            _window.Resumed -= OnWindowResumed;
            _window = null;
        }
    }

    private void OnWindowResumed(object? sender, EventArgs e) => _viewModel.RefreshNotificationsCommand.Execute(null);
}
