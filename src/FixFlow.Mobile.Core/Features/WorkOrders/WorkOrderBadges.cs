using FixFlow.Mobile.Core.Api;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public static class WorkOrderBadges
{
    public static BadgeTone ForStatus(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Assigned => BadgeTone.Info,
        WorkOrderStatus.InProgress => BadgeTone.Warning,
        WorkOrderStatus.Completed => BadgeTone.Success,
        WorkOrderStatus.Invoiced => BadgeTone.Outline,
        _ => BadgeTone.Neutral,
    };

    public static BadgeTone ForPriority(WorkOrderPriority priority) => priority switch
    {
        WorkOrderPriority.Critical => BadgeTone.Destructive,
        WorkOrderPriority.High => BadgeTone.Caution,
        _ => BadgeTone.Neutral,
    };
}
