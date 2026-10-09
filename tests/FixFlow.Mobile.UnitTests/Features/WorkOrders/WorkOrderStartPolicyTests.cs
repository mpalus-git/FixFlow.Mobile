using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.Sync.Outbox;
using FixFlow.Mobile.Core.Features.WorkOrders;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class WorkOrderStartPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_Allow_Start_When_Work_Order_Is_Assigned_And_No_Other_Is_In_Progress()
    {
        var workOrder = TestWorkOrders.Create("ZL-1", Now);
        var completed = TestWorkOrders.Create("ZL-2", Now, WorkOrderStatus.Completed, Now);

        WorkOrderStartPolicy.Evaluate(workOrder, [workOrder, completed]).ShouldBe(WorkOrderStartAvailability.Available);
    }

    [Theory]
    [InlineData(WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.Invoiced)]
    public void Should_Not_Offer_Start_When_Work_Order_Is_Not_Assigned(WorkOrderStatus status)
    {
        var workOrder = TestWorkOrders.Create("ZL-1", Now, status);

        var availability = WorkOrderStartPolicy.Evaluate(workOrder, [workOrder]);

        availability.CanStart.ShouldBeFalse();
        availability.IsBlocked.ShouldBeFalse();
    }

    [Fact]
    public void Should_Block_Start_With_Number_When_Other_Work_Order_Is_In_Progress()
    {
        var workOrder = TestWorkOrders.Create("ZL-1", Now);
        var inProgress = TestWorkOrders.Create("ZL-2", Now, WorkOrderStatus.InProgress);

        WorkOrderStartPolicy.Evaluate(workOrder, [workOrder, inProgress]).ShouldBe(new WorkOrderStartAvailability(false, "ZL-2"));
    }

    [Fact]
    public void Should_Block_Start_When_Other_Work_Order_Was_Started_Only_In_Queue()
    {
        var workOrder = TestWorkOrders.Create("ZL-1", Now);
        var other = TestWorkOrders.Create("ZL-2", Now);
        var start = new OutboxOperation(Guid.CreateVersion7(), other.Id, OutboxOperationKind.StartWork, OutboxPayload.Empty, Now, 0, null, null);

        var overlaid = PendingOperationsOverlay.Apply([workOrder, other], [start]);

        WorkOrderStartPolicy.Evaluate(workOrder, overlaid).BlockingWorkOrderNumber.ShouldBe("ZL-2");
    }
}
