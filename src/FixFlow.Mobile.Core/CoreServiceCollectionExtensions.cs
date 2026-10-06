using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.Parts;
using FixFlow.Mobile.Core.Features.ServiceEntries;
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
        services.AddTransient<OutboxRepository>();
        services.AddTransient<OutboxWriter>();
        services.AddSingleton(OutboxRetryPolicy.CreateDefault());
        services.AddTransient<OutboxDispatcher>();
        services.AddSingleton(provider => new SyncCoordinator(
            provider.GetRequiredService<UserDatabase>(),
            provider.GetRequiredService<IConnectivityService>(),
            provider.GetRequiredService<ServerWarmup>(),
            provider.GetRequiredService<ServerReadinessProbe>,
            provider.GetRequiredService<OutboxDispatcher>,
            provider.GetRequiredService<WorkOrderSyncService>,
            provider.GetRequiredService<PartSyncService>,
            provider.GetRequiredService<IMessenger>()));
        services.AddTransient<WorkOrderSyncService>();
        services.AddTransient<PartSyncService>();
        services.AddTransient<WorkOrderDetailsSyncService>();
        services.AddTransient<WorkOrderRepository>();
        services.AddTransient<ClientRepository>();
        services.AddTransient<DeviceRepository>();
        services.AddTransient<DeviceHistoryRepository>();
        services.AddTransient<ServiceProtocolService>();
        services.AddTransient<ServiceEntryRepository>();
        services.AddTransient<ServiceEntryPartsSelection>();
        services.AddTransient<WorkTimerRepository>();
        services.AddTransient<WorkLocationRecorder>();
        services.AddSingleton<WorkOrderSectionClassifier>();
        services.AddSingleton<WorkOrderListPresenter>();
        services.AddSingleton<WorkOrderDetailsPresenter>();
        services.AddTransient<WorkOrderListViewModel>();
        services.AddTransient<WorkOrderDetailsSources>();
        services.AddTransient<WorkOrderDetailsViewModel>();

        return services;
    }
}
