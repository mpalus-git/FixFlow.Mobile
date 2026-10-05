using FixFlow.Mobile.Core.Features.Settings;

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
