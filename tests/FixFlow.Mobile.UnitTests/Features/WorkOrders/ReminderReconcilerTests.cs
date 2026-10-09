using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync.Outbox;
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
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly ISettingsStore _settings = Substitute.For<ISettingsStore>();
    private readonly DueDateReminderPlanner _planner;
    private readonly WorkOrderRepository _workOrders;
    private readonly OutboxRepository _outbox;
    private readonly ReminderReconciler _reconciler;

    public ReminderReconcilerTests()
    {
        var clock = BusinessClock.CreateForWarsaw(_timeProvider);
        _planner = new DueDateReminderPlanner(clock, new DisplayFormatter(new LanguageService(Substitute.For<ISettingsStore>()), clock));
        _workOrders = new WorkOrderRepository(_userDatabase.Database);
        _outbox = new OutboxRepository(_userDatabase.Database);
        _settings.GetString(Arg.Any<string>()).Returns((string?)null);
        _reconciler = CreateReconciler(_scheduler);
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

    [Fact]
    public async Task Should_Cancel_And_Not_Schedule_When_Notifications_Are_Not_Allowed()
    {
        var blocked = new FakeNotificationScheduler { IsEnabled = false };
        await blocked.ScheduleAsync(new ScheduledReminder(7, Guid.NewGuid(), Now.AddHours(3), "Za godzinę", "ZL-STARE"));
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);
        await _workOrders.ReplaceAllAsync([TestWorkOrders.Create("ZL-2026-0042", Now.AddHours(5))]);

        await CreateReconciler(blocked).ReconcileAsync();

        await _dialogs.ReceivedWithAnyArgs(1).ConfirmAsync(default!, default!, default!, default!);
        blocked.Scheduled.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Not_Ask_For_Consent_When_There_Is_Nothing_To_Remind()
    {
        var blocked = new FakeNotificationScheduler { IsEnabled = false };
        await _workOrders.ReplaceAllAsync([TestWorkOrders.Create("ZL-2026-0042", Now.AddMinutes(30))]);

        await CreateReconciler(blocked).ReconcileAsync();

        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task Should_Do_Nothing_When_Platform_Does_Not_Support_Notifications()
    {
        var unsupported = new FakeNotificationScheduler { IsSupported = false, IsEnabled = false };
        await _workOrders.ReplaceAllAsync([TestWorkOrders.Create("ZL-2026-0042", Now.AddHours(5))]);

        await CreateReconciler(unsupported).ReconcileAsync();

        unsupported.ScheduleCalls.ShouldBe(0);
        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();

    private ReminderReconciler CreateReconciler(FakeNotificationScheduler scheduler) =>
        new(_userDatabase.Database, _workOrders, _outbox, _planner, scheduler, new NotificationConsent(scheduler, _settings, _dialogs));
}
