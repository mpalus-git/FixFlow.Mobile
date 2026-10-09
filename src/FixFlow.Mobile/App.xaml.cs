using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;

namespace FixFlow.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly ThemeService _themeService;

    public App(IServiceProvider services, ILanguageService languageService, ThemeService themeService)
    {
        InitializeComponent();
        _services = services;
        _themeService = themeService;
        UserAppTheme = ToAppTheme(themeService.CurrentTheme);
        languageService.LanguageChanged += OnLanguageChanged;
        themeService.ThemeChanged += OnThemeChanged;
        RequestedThemeChanged += OnRequestedThemeChanged;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(CreateShell());
        window.Created += (_, _) => SystemBars.ApplyTheme(window, RequestedTheme);
        return window;
    }

    private AppShell CreateShell() => _services.GetRequiredService<AppShell>();

    protected override void OnSleep()
    {
        _services.GetRequiredService<SyncCoordinator>().SuspendRetries();
        base.OnSleep();
    }

    protected override async void OnResume()
    {
        base.OnResume();
        await _services.GetRequiredService<SyncCoordinator>().ResumeAsync();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var window in Windows)
        {
            window.Page = CreateShell();
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e) => UserAppTheme = ToAppTheme(_themeService.CurrentTheme);

    private static AppTheme ToAppTheme(AppThemePreference theme) => theme switch
    {
        AppThemePreference.Light => AppTheme.Light,
        AppThemePreference.Dark => AppTheme.Dark,
        _ => AppTheme.Unspecified,
    };

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        foreach (var window in Windows)
        {
            SystemBars.ApplyTheme(window, e.RequestedTheme);
        }
    }
}
