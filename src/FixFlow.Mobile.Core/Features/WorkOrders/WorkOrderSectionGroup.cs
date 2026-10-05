namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed record WorkOrderSectionGroup(WorkOrderSection Section, IReadOnlyList<WorkOrder> WorkOrders);
