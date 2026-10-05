using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.Mobile.Core;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(provider => BusinessClock.CreateForWarsaw(provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ILanguageService, LanguageService>();
        services.AddSingleton<DisplayFormatter>();

        services.AddTransient<WorkOrderListViewModel>();

        return services;
    }
}
