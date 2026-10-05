using FixFlow.Mobile.Core;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Features.Settings;
using FixFlow.Mobile.Features.WorkOrders;

namespace FixFlow.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddCore();
        builder.Services.AddSingleton(Preferences.Default);
        builder.Services.AddSingleton<ISettingsStore, PreferencesSettingsStore>();
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<WorkOrderListPage>();

        var app = builder.Build();
        app.Services.GetRequiredService<ILanguageService>().ApplySavedLanguage();

        return app;
    }
}
