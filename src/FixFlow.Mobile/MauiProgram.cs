using CommunityToolkit.Maui;
using FixFlow.Mobile.Accessibility;
using FixFlow.Mobile.Clipboard;
using FixFlow.Mobile.Connectivity;
using FixFlow.Mobile.Core.Accessibility;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Clipboard;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Dispatching;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Launching;
using FixFlow.Mobile.Core.Location;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Notifications;
using FixFlow.Mobile.Core.Photos;
using FixFlow.Mobile.Core.Storage;
using FixFlow.Mobile.Core;
using FixFlow.Mobile.Dialogs;
using FixFlow.Mobile.Dispatching;
using FixFlow.Mobile.Features.Auth;
using FixFlow.Mobile.Features.ServiceEntries;
using FixFlow.Mobile.Features.Settings;
using FixFlow.Mobile.Features.Sync;
using FixFlow.Mobile.Features.WorkOrders;
using FixFlow.Mobile.Launching;
using FixFlow.Mobile.Location;
using FixFlow.Mobile.Navigation;
using FixFlow.Mobile.Photos;

namespace FixFlow.Mobile;

public static partial class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureMauiHandlers(ConfigurePlatformHandlers)
            .ConfigureImageSources(ConfigurePlatformImageSources);

        ConfigurePlatformServices(builder);
        builder.Services.AddCore();
        builder.Services.AddFixFlowApi(new Uri(BuildConfiguration.ApiBaseUrl));
        builder.Services.AddSingleton(Preferences.Default);
        builder.Services.AddSingleton<ISettingsStore, PreferencesSettingsStore>();
        builder.Services.AddSingleton<IAppVersion, BuildAppVersion>();
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
        builder.Services.AddSingleton(Geolocation.Default);
        builder.Services.AddSingleton<ILocationService, EssentialsLocationService>();
        builder.Services.AddSingleton(MediaPicker.Default);
        builder.Services.AddSingleton<IPhotoService, EssentialsPhotoService>();
        builder.Services.AddSingleton<IUiDispatcher, MainThreadUiDispatcher>();
        builder.Services.AddSingleton(Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default);
        builder.Services.AddSingleton<IClipboardService, EssentialsClipboardService>();
        builder.Services.AddSingleton(SemanticScreenReader.Default);
        builder.Services.AddSingleton<IScreenReaderAnnouncer, SemanticScreenReaderAnnouncer>();
        builder.Services.AddSingleton(new DemoAccount(BuildConfiguration.DemoTechnicianEmail, BuildConfiguration.DemoTechnicianPassword));
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IDialogService, PageDialogService>();
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<StartupPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<WorkOrderListPage>();
        builder.Services.AddTransient<WorkOrderDetailsPage>();
        builder.Services.AddTransient<CompleteWorkOrderPage>();
        builder.Services.AddTransient<ServiceEntryPage>();
        builder.Services.AddTransient<CorrectionEntryPage>();
        builder.Services.AddTransient<SyncPage>();
        builder.Services.AddTransient<SettingsPage>();

        var app = builder.Build();
        app.Services.GetRequiredService<ILanguageService>().ApplySavedLanguage();
        app.Services.GetRequiredService<ThemeService>().ApplySavedTheme();
        app.Services.GetRequiredService<SessionEndNavigator>().Start();
        app.Services.GetRequiredService<SyncCoordinator>().Start();
        app.Services.GetRequiredService<ConnectivityAnnouncer>().Start();
        app.Services.GetRequiredService<ReminderCoordinator>().Start();

        return app;
    }

    private static partial void ConfigurePlatformServices(MauiAppBuilder builder);

    private static partial void ConfigurePlatformHandlers(IMauiHandlersCollection handlers);

    private static partial void ConfigurePlatformImageSources(IImageSourceServiceCollection services);
}
