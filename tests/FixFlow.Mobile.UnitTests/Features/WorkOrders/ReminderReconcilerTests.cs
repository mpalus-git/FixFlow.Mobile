using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Notifications;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class ReminderReconcilerTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly TestUserDatabase _userDatabase = new();
    private readonly FakeNotificationScheduler _scheduler = new();
    private readonly WorkOrderRepository _workOrders;
    private readonly OutboxRepository _outbox;
    private readonly ReminderReconciler _reconciler;

    public ReminderReconcilerTests()
    {
        var clock = BusinessClock.CreateForWarsaw(_timeProvider);
        var planner = new DueDateReminderPlanner(clock, new DisplayFormatter(new LanguageService(Substitute.For<ISettingsStore>()), clock));
        _workOrders = new WorkOrderRepository(_userDatabase.Database);
        _outbox = new OutboxRepository(_userDatabase.Database);
        _reconciler = new ReminderReconciler(_userDatabase.Database, _workOrders, _outbox, planner, _scheduler);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Schedule_Reminder_When_Assigned_Work_Order_Is_Stored()
    {
        var workOrder = TestWorkOrders.Create("ZL-2026-0042", Now.AddHours(5));
        await _workOrders.ReplaceAllAsync([workOrder]);

        await _reconciler.ReconcileAsync();

        _scheduler.Scheduled.ShouldHaveSingleItem().NotifyAt.ShouldBe(Now.AddHours(4));
    }

    [Fact]
    public async Task Should_Not_Schedule_Again_When_Reminder_Did_Not_Change()
    {
        await _workOrders.ReplaceAllAsync([TestWorkOrders.Create("ZL-2026-0042", Now.AddHours(5))]);
        await _reconciler.ReconcileAsync();

        await _reconciler.ReconcileAsync();

        _scheduler.ScheduleCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Reschedule_Reminder_When_Dispatcher_Changed_Due_Date()
    {
        var workOrder = TestWorkOrders.Create("ZL-2026-0042", Now.AddHours(5));
        await _workOrders.ReplaceAllAsync([workOrder]);
        await _reconciler.ReconcileAsync();

        await _workOrders.ReplaceAllAsync([workOrder with { DueDate = Now.AddDays(1) }]);
        await _reconciler.ReconcileAsync();

        _scheduler.Scheduled.ShouldHaveSingleItem().NotifyAt.ShouldBe(Now.AddDays(1).AddHours(-1));
    }

    [Fact]
    public async Task Should_Cancel_Reminder_When_Work_Order_Was_Taken_Away()
    {
        await _workOrders.ReplaceAllAsync([TestWorkOrders.Create("ZL-2026-0042", Now.AddHours(5))]);
        await _reconciler.ReconcileAsync();

        await _workOrders.ReplaceAllAsync([]);
        await _reconciler.ReconcileAsync();

        _scheduler.Scheduled.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Cancel_Reminder_When_Completion_Waits_In_Queue()
    {
        var workOrder = TestWorkOrders.Create("ZL-2026-0042", Now.AddHours(5), WorkOrderStatus.InProgress);
        await _workOrders.ReplaceAllAsync([workOrder]);
        await _reconciler.ReconcileAsync();

        await new OutboxWriter(_outbox, _timeProvider, new WeakReferenceMessenger()).CompleteWorkOrderAsync(workOrder.Id);
        await _reconciler.ReconcileAsync();

        _scheduler.Scheduled.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Cancel_Reminder_Of_Previous_Technician_When_Other_Technician_Uses_Device()
    {
        await _scheduler.ScheduleAsync(new ScheduledReminder(7, Guid.NewGuid(), Now.AddHours(3), "Za godzinę", "ZL-OBCE"));

        await _reconciler.ReconcileAsync();

        _scheduler.Scheduled.ShouldBeEmpty();
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();
}
