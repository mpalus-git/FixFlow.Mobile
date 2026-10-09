namespace FixFlow.Mobile.Core.Features.WorkOrders.List;

public sealed record WorkOrderSectionGroup(WorkOrderSection Section, IReadOnlyList<WorkOrder> WorkOrders);
