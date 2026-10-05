using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Screens;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.Mobile.Core;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(provider => BusinessClock.CreateForWarsaw(provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ILanguageService, LanguageService>();
        services.AddSingleton<SessionStore>();
        services.AddSingleton<DisplayFormatter>();
        services.AddTransient<ScreenState>();

        services.AddTransient<WorkOrderListViewModel>();

        return services;
    }
}
