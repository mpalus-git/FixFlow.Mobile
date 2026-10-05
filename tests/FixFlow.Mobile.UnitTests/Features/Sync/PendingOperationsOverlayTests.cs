using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.UnitTests.Features.WorkOrders;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

public sealed class PendingOperationsOverlayTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_Show_Work_Order_In_Progress_When_Start_Waits_In_Queue()
    {
        var workOrder = TestWorkOrders.Create("ZL-1", Now.AddHours(2));

        var overlaid = PendingOperationsOverlay.Apply(workOrder, [Operation(workOrder.Id, OutboxOperationKind.StartWork, Now)]);

        overlaid.Status.ShouldBe(WorkOrderStatus.InProgress);
        overlaid.StartedAt.ShouldBe(Now);
    }

    [Fact]
    public void Should_Show_Work_Order_Completed_When_Start_And_Completion_Wait_In_Queue()
    {
        var workOrder = TestWorkOrders.Create("ZL-1", Now.AddHours(-2), isOverdue: true);

        var overlaid = PendingOperationsOverlay.Apply(workOrder, [
            Operation(workOrder.Id, OutboxOperationKind.StartWork, Now),
            Operation(workOrder.Id, OutboxOperationKind.CompleteWorkOrder, Now.AddHours(1)),
        ]);

        overlaid.Status.ShouldBe(WorkOrderStatus.Completed);
        overlaid.CompletedAt.ShouldBe(Now.AddHours(1));
        overlaid.IsOverdue.ShouldBeFalse();
    }

    [Fact]
    public void Should_Keep_Server_State_When_Server_Already_Moved_Further()
    {
        var invoiced = TestWorkOrders.Create("ZL-1", Now, WorkOrderStatus.Invoiced, Now.AddDays(-1));

        var overlaid = PendingOperationsOverlay.Apply(invoiced, [Operation(invoiced.Id, OutboxOperationKind.CompleteWorkOrder, Now)]);

        overlaid.ShouldBe(invoiced);
    }

    [Fact]
    public void Should_Apply_Only_Operations_Of_Each_Work_Order_When_List_Is_Overlaid()
    {
        var started = TestWorkOrders.Create("ZL-1", Now);
        var untouched = TestWorkOrders.Create("ZL-2", Now);

        var overlaid = PendingOperationsOverlay.Apply([started, untouched], [Operation(started.Id, OutboxOperationKind.StartWork, Now)]);

        overlaid.Select(workOrder => workOrder.Status).ShouldBe([WorkOrderStatus.InProgress, WorkOrderStatus.Assigned]);
    }

    [Fact]
    public void Should_Return_Queued_Entries_When_Entries_Wait_In_Queue()
    {
        var entry = new PendingServiceEntry(Guid.CreateVersion7(), "Wymiana filtra", false, null, null, null, null, []);
        var operation = Operation(Guid.NewGuid(), OutboxOperationKind.AddServiceEntry, Now) with { Payload = OutboxPayload.Serialize(entry) };

        PendingOperationsOverlay.PendingEntries([operation, Operation(operation.WorkOrderId, OutboxOperationKind.StartWork, Now)])
            .ShouldHaveSingleItem().Id.ShouldBe(entry.Id);
    }

    private static OutboxOperation Operation(Guid workOrderId, OutboxOperationKind kind, DateTimeOffset createdAt) =>
        new(Guid.CreateVersion7(), workOrderId, kind, OutboxPayload.Empty, createdAt, 0, null, null);
}
