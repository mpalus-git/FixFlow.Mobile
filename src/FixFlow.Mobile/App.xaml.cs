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
    }

    protected override Window CreateWindow(IActivationState? activationState) => new(CreateShell());

    private AppShell CreateShell() => _services.GetRequiredService<AppShell>();

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var window in Windows)
        {
            window.Page = CreateShell();
        }
    }
}
