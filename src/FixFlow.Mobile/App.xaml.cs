using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;

namespace FixFlow.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services, ILanguageService languageService)
    {
        InitializeComponent();
        _services = services;
        languageService.LanguageChanged += OnLanguageChanged;
        RequestedThemeChanged += OnRequestedThemeChanged;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(CreateShell());
        window.Created += (_, _) => SystemBars.ApplyTheme(window, RequestedTheme);
        return window;
    }

    private AppShell CreateShell() => _services.GetRequiredService<AppShell>();

    protected override async void OnResume()
    {
        base.OnResume();
        await _services.GetRequiredService<SyncCoordinator>().SynchronizeInBackgroundAsync();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var window in Windows)
        {
            window.Page = CreateShell();
        }
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        foreach (var window in Windows)
        {
            SystemBars.ApplyTheme(window, e.RequestedTheme);
        }
    }
}
