using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.Parts;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Screens;
using FixFlow.Mobile.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.Mobile.Core;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton(provider => BusinessClock.CreateForWarsaw(provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ILanguageService, LanguageService>();
        services.AddSingleton<SessionStore>();
        services.AddSingleton<UserDatabase>();
        services.AddSingleton<ServerWarmup>();
        services.AddSingleton<SessionEndNavigator>();
        services.AddSingleton<DisplayFormatter>();
        services.AddTransient<ScreenState>();

        services.AddTransient<StartupViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<PartRepository>();
        services.AddTransient<SyncStateRepository>();
        services.AddTransient<WorkOrderSyncService>();
        services.AddTransient<WorkOrderRepository>();
        services.AddTransient<ClientRepository>();
        services.AddTransient<DeviceRepository>();
        services.AddSingleton<WorkOrderSectionClassifier>();
        services.AddSingleton<WorkOrderListPresenter>();
        services.AddTransient<WorkOrderListViewModel>();

        return services;
    }
}
