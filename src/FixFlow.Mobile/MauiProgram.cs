using FixFlow.Mobile.Connectivity;
using FixFlow.Mobile.Core;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Launching;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Storage;
using FixFlow.Mobile.Dialogs;
using FixFlow.Mobile.Features.Auth;
using FixFlow.Mobile.Features.Settings;
using FixFlow.Mobile.Features.WorkOrders;
using FixFlow.Mobile.Launching;
using FixFlow.Mobile.Navigation;

namespace FixFlow.Mobile;

public static partial class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureMauiHandlers(ConfigurePlatformHandlers);

        builder.Services.AddCore();
        builder.Services.AddFixFlowApi(new Uri(BuildConfiguration.ApiBaseUrl));
        builder.Services.AddSingleton(Preferences.Default);
        builder.Services.AddSingleton<ISettingsStore, PreferencesSettingsStore>();
        builder.Services.AddSingleton(SecureStorage.Default);
        builder.Services.AddSingleton<ISecureStore, SecureStorageStore>();
        builder.Services.AddSingleton(new LocalDatabaseOptions(FileSystem.AppDataDirectory));
        builder.Services.AddSingleton(Microsoft.Maui.Networking.Connectivity.Current);
        builder.Services.AddSingleton<IConnectivityService, EssentialsConnectivityService>();
        builder.Services.AddSingleton(Launcher.Default);
        builder.Services.AddSingleton<IUriLauncher, EssentialsUriLauncher>();
        builder.Services.AddSingleton(Map.Default);
        builder.Services.AddSingleton<IMapLauncher, EssentialsMapLauncher>();
        builder.Services.AddSingleton(FileSystem.Current);
        builder.Services.AddSingleton<IDocumentLauncher, EssentialsDocumentLauncher>();
        builder.Services.AddSingleton(new DemoAccount(BuildConfiguration.DemoTechnicianEmail, BuildConfiguration.DemoTechnicianPassword));
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IDialogService, PageDialogService>();
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<StartupPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<WorkOrderListPage>();

        var app = builder.Build();
        app.Services.GetRequiredService<ILanguageService>().ApplySavedLanguage();
        app.Services.GetRequiredService<SessionEndNavigator>().Start();

        return app;
    }

    private static partial void ConfigurePlatformHandlers(IMauiHandlersCollection handlers);
}
