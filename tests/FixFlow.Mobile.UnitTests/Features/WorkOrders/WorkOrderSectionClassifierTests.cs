using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using Microsoft.Extensions.Time.Testing;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class WorkOrderSectionClassifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 21, 30, 0, TimeSpan.Zero);

    private readonly WorkOrderSectionClassifier _classifier;

    public WorkOrderSectionClassifierTests()
    {
        var timeProvider = new FakeTimeProvider(Now);
        timeProvider.SetLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
        _classifier = new WorkOrderSectionClassifier(BusinessClock.CreateForWarsaw(timeProvider));
    }

    [Fact]
    public void Should_Classify_As_Today_When_Due_Later_Today_In_Warsaw()
    {
        _classifier.Classify(TestWorkOrders.Create("A", Now.AddMinutes(20))).ShouldBe(WorkOrderSection.Today);
    }

    [Fact]
    public void Should_Classify_As_Upcoming_When_Due_After_Warsaw_Midnight_Despite_Same_Utc_Date()
    {
        var dueDate = new DateTimeOffset(2026, 10, 5, 22, 30, 0, TimeSpan.Zero);

        _classifier.Classify(TestWorkOrders.Create("A", dueDate)).ShouldBe(WorkOrderSection.Upcoming);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, -26)]
    public void Should_Classify_As_Overdue_When_Api_Flags_It_Or_Due_Day_Has_Passed(bool isOverdue, int dueInHours)
    {
        var workOrder = TestWorkOrders.Create("A", Now.AddHours(dueInHours), WorkOrderStatus.InProgress, isOverdue: isOverdue);

        _classifier.Classify(workOrder).ShouldBe(WorkOrderSection.Overdue);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Completed, 3, WorkOrderSection.RecentlyCompleted)]
    [InlineData(WorkOrderStatus.Invoiced, 1, WorkOrderSection.RecentlyCompleted)]
    [InlineData(WorkOrderStatus.Completed, 8, null)]
    public void Should_Show_Completed_Work_Order_Only_When_Completed_Within_7_Days(
        WorkOrderStatus status,
        int completedDaysAgo,
        WorkOrderSection? expected)
    {
        var workOrder = TestWorkOrders.Create("A", Now.AddDays(-10), status, completedAt: Now.AddDays(-completedDaysAgo));

        _classifier.Classify(workOrder).ShouldBe(expected);
    }

    [Fact]
    public void Should_Group_In_Fixed_Section_Order_And_Skip_Empty_Sections_When_Grouping()
    {
        var upcomingLater = TestWorkOrders.Create("U2", Now.AddDays(3));
        var upcomingSooner = TestWorkOrders.Create("U1", Now.AddDays(1));
        var overdue = TestWorkOrders.Create("O1", Now.AddDays(-2));
        var completedEarlier = TestWorkOrders.Create("C2", Now.AddDays(-9), WorkOrderStatus.Completed, Now.AddDays(-4));
        var completedLater = TestWorkOrders.Create("C1", Now.AddDays(-9), WorkOrderStatus.Invoiced, Now.AddDays(-1));

        var groups = _classifier.Group([upcomingLater, completedEarlier, overdue, upcomingSooner, completedLater]);

        groups.Select(group => group.Section).ShouldBe([WorkOrderSection.Overdue, WorkOrderSection.Upcoming, WorkOrderSection.RecentlyCompleted]);
        groups[1].WorkOrders.Select(workOrder => workOrder.Number).ShouldBe(["U1", "U2"]);
        groups[2].WorkOrders.Select(workOrder => workOrder.Number).ShouldBe(["C1", "C2"]);
    }
}
