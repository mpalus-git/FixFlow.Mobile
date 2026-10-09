using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.WorkOrders.List;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public static class WorkOrderTexts
{
    public static string Status(WorkOrderStatus status) => Text($"WorkOrderStatus{status}");

    public static string Priority(WorkOrderPriority priority) => Text($"WorkOrderPriority{priority}");

    public static string Section(WorkOrderSection section) => Text($"WorkOrderSection{section}");

    public static bool IsActive(WorkOrderStatus status) => status is WorkOrderStatus.Assigned or WorkOrderStatus.InProgress;

    private static string Text(string key) => AppResources.ResourceManager.GetString(key, AppResources.Culture) ?? key;
}
