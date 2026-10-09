using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class WorkOrderActionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_Allow_Only_Start_When_Assigned_And_No_Other_Work_Is_In_Progress()
    {
        var workOrder = TestWorkOrders.Create("ZL-1", Now);

        var actions = WorkOrderActions.Evaluate(workOrder, [workOrder], [], []);

        actions.ShouldBe(new WorkOrderActions(true, null, false, false, false, null));
    }

    [Fact]
    public void Should_Name_Blocking_Work_Order_When_Other_Work_Is_In_Progress()
    {
        var workOrder = TestWorkOrders.Create("ZL-1", Now);
        var inProgress = TestWorkOrders.Create("ZL-2", Now, WorkOrderStatus.InProgress);

        var actions = WorkOrderActions.Evaluate(workOrder, [workOrder, inProgress], [], []);

        actions.CanStartWork.ShouldBeFalse();
        actions.StartBlockedText.ShouldBe(AppText.Format(AppResources.StartWorkBlocked, "ZL-2"));
        actions.HasStartBlockedText.ShouldBeTrue();
    }

    [Fact]
    public void Should_Require_Entry_Before_Completion_When_Work_Has_No_Entries()
    {
        var workOrder = TestWorkOrders.Create("ZL-1", Now, WorkOrderStatus.InProgress);

        var actions = WorkOrderActions.Evaluate(workOrder, [workOrder], [], []);

        actions.ShouldBe(new WorkOrderActions(false, null, true, false, false, AppResources.CompleteRequiresEntry));
        actions.HasCompleteBlockedText.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, true)]
    public void Should_Allow_Completion_And_Correction_Only_With_Used_Parts_When_Entry_Is_Pending(int quantity, bool canCorrect)
    {
        var workOrder = TestWorkOrders.Create("ZL-1", Now, WorkOrderStatus.InProgress);
        PendingServiceEntryPart[] parts = quantity > 0 ? [new PendingServiceEntryPart(Guid.NewGuid(), "Filtr", "FP-100", quantity, 12m)] : [];
        var entry = new PendingServiceEntry(Guid.CreateVersion7(), "Wymiana", false, Now, Now.AddMinutes(30), null, null, parts);

        var actions = WorkOrderActions.Evaluate(workOrder, [workOrder], [], [entry]);

        actions.CanComplete.ShouldBeTrue();
        actions.CompleteBlockedText.ShouldBeNull();
        actions.CanAddCorrection.ShouldBe(canCorrect);
    }

    [Fact]
    public void Should_Allow_Nothing_When_Work_Order_Is_Completed()
    {
        var workOrder = TestWorkOrders.Create("ZL-1", Now, WorkOrderStatus.Completed, Now);

        WorkOrderActions.Evaluate(workOrder, [workOrder], [], []).ShouldBe(WorkOrderActions.None);
    }
}
