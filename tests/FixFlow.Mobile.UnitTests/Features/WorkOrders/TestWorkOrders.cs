using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.WorkOrders;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

internal static class TestWorkOrders
{
    public static WorkOrder Create(
        string number,
        DateTimeOffset dueDate,
        WorkOrderStatus status = WorkOrderStatus.Assigned,
        DateTimeOffset? completedAt = null,
        bool isOverdue = false) =>
        new(
            Guid.NewGuid(),
            number,
            Guid.NewGuid(),
            "SN-123",
            "Daikin Altherma 3",
            Guid.NewGuid(),
            "Piekarnia Kowalscy",
            "Przegląd pompy ciepła",
            WorkOrderPriority.Normal,
            status,
            dueDate,
            isOverdue,
            dueDate.AddDays(-7),
            null,
            completedAt,
            null);
}
