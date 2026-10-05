using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.WorkOrders;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.Mobile.Core;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton<ILanguageService, LanguageService>();

        services.AddTransient<WorkOrderListViewModel>();

        return services;
    }
}
