using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.WorkOrders;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class WorkOrderBadgesTests
{
    [Theory]
    [InlineData(WorkOrderStatus.New, BadgeTone.Neutral)]
    [InlineData(WorkOrderStatus.Assigned, BadgeTone.Info)]
    [InlineData(WorkOrderStatus.InProgress, BadgeTone.Warning)]
    [InlineData(WorkOrderStatus.Completed, BadgeTone.Success)]
    [InlineData(WorkOrderStatus.Invoiced, BadgeTone.Outline)]
    public void Should_Use_Panel_Status_Tone_When_Status_Is_Given(WorkOrderStatus status, BadgeTone expected) =>
        WorkOrderBadges.ForStatus(status).ShouldBe(expected);

    [Theory]
    [InlineData(WorkOrderPriority.Low, BadgeTone.Neutral)]
    [InlineData(WorkOrderPriority.Normal, BadgeTone.Neutral)]
    [InlineData(WorkOrderPriority.High, BadgeTone.Caution)]
    [InlineData(WorkOrderPriority.Critical, BadgeTone.Destructive)]
    public void Should_Use_Panel_Priority_Tone_When_Priority_Is_Given(WorkOrderPriority priority, BadgeTone expected) =>
        WorkOrderBadges.ForPriority(priority).ShouldBe(expected);
}
