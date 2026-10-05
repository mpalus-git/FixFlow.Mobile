using FixFlow.Mobile.Core;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Dialogs;
using FixFlow.Mobile.Features.Settings;
using FixFlow.Mobile.Features.WorkOrders;
using FixFlow.Mobile.Navigation;

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
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IDialogService, PageDialogService>();
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<WorkOrderListPage>();

        var app = builder.Build();
        app.Services.GetRequiredService<ILanguageService>().ApplySavedLanguage();

        return app;
    }
}
