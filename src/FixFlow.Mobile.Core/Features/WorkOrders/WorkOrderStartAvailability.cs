namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed record WorkOrderStartAvailability(bool CanStart, string? BlockingWorkOrderNumber)
{
    public static WorkOrderStartAvailability NotAvailable { get; } = new(false, null);

    public static WorkOrderStartAvailability Available { get; } = new(true, null);

    public bool IsBlocked => BlockingWorkOrderNumber is not null;
}
