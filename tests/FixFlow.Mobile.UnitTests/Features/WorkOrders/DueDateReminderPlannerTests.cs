using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Notifications;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

[Collection(GlobalCultureTests.Name)]
public sealed class DueDateReminderPlannerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    private readonly GlobalCultureScope _cultureScope = new();
    private readonly DueDateReminderPlanner _planner;

    public DueDateReminderPlannerTests()
    {
        var languageService = new LanguageService(Substitute.For<ISettingsStore>());
        languageService.ChangeLanguage(AppLanguage.Polish);
        var clock = BusinessClock.CreateForWarsaw(new FakeTimeProvider(Now));
        _planner = new DueDateReminderPlanner(clock, new DisplayFormatter(languageService, clock));
    }

    [Fact]
    public void Should_Plan_Reminder_One_Hour_Before_Due_Date_With_Warsaw_Time_When_Work_Order_Is_Assigned()
    {
        var workOrder = TestWorkOrders.Create("ZL-2026-0042", Now.AddHours(8));

        var reminder = _planner.Plan([workOrder]).ShouldHaveSingleItem();

        reminder.WorkOrderId.ShouldBe(workOrder.Id);
        reminder.NotifyAt.ShouldBe(Now.AddHours(7));
        reminder.Title.ShouldBe("Za godzinę termin zlecenia");
        reminder.Message.ShouldBe("ZL-2026-0042 · Piekarnia Kowalscy · termin 18:00");
    }

    [Fact]
    public void Should_Plan_Reminder_When_Work_Order_Is_In_Progress()
    {
        var workOrder = TestWorkOrders.Create("ZL-2026-0042", Now.AddHours(2), WorkOrderStatus.InProgress);

        _planner.Plan([workOrder]).ShouldHaveSingleItem().NotifyAt.ShouldBe(Now.AddHours(1));
    }

    [Theory]
    [InlineData(WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.Invoiced)]
    public void Should_Skip_Work_Order_When_It_Is_No_Longer_Active(WorkOrderStatus status)
    {
        _planner.Plan([TestWorkOrders.Create("ZL-2026-0042", Now.AddHours(8), status, Now)]).ShouldBeEmpty();
    }

    [Fact]
    public void Should_Skip_Work_Order_When_Reminder_Time_Has_Already_Passed()
    {
        _planner.Plan([TestWorkOrders.Create("ZL-2026-0042", Now.AddMinutes(59))]).ShouldBeEmpty();
    }

    [Fact]
    public void Should_Keep_Same_Notification_Id_When_Work_Order_Is_Planned_Again()
    {
        var workOrderId = Guid.CreateVersion7();

        var first = DueDateReminderPlanner.NotificationIdFor(workOrderId);

        DueDateReminderPlanner.NotificationIdFor(workOrderId).ShouldBe(first);
        first.ShouldBeGreaterThanOrEqualTo(0);
    }

    public void Dispose() => _cultureScope.Dispose();
}
