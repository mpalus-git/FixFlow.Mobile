using FixFlow.Mobile.Core;
using FixFlow.Mobile.Features.WorkOrders;

namespace FixFlow.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddCore();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<WorkOrderListPage>();

        return builder.Build();
    }
}
