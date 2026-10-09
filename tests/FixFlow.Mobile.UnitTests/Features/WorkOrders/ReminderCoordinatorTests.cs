using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Notifications;
using FixFlow.Mobile.UnitTests.Features.Auth;
using Microsoft.Extensions.Time.Testing;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class ReminderCoordinatorTests : IDisposable
{
    private readonly FakeNotificationScheduler _scheduler = new();
    private readonly SessionStore _session = new(new InMemorySecureStore(), new FakeTimeProvider());
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly ReminderCoordinator _coordinator;
    private int _reconcileRequests;

    public ReminderCoordinatorTests()
    {
        _coordinator = new ReminderCoordinator(_session, _messenger, CreateReconciler, _scheduler);
        _coordinator.Start();
    }

    [Fact]
    public async Task Should_Reconcile_Reminders_When_Synchronization_Completes()
    {
        _messenger.Send(new SyncCompletedMessage(SyncResult.Offline));

        await WaitUntilAsync(() => Volatile.Read(ref _reconcileRequests) == 1);
    }

    [Fact]
    public async Task Should_Cancel_All_Reminders_When_Technician_Signs_Out()
    {
        await _scheduler.ScheduleAsync(new ScheduledReminder(7, Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(3), "Za godzinę", "ZL-2026-0042"));

        _session.End(SessionEndReason.SignedOut);

        await WaitUntilAsync(() => _scheduler.Scheduled.Count == 0);
    }

    [Fact]
    public async Task Should_Keep_Reminders_When_Session_Expires()
    {
        await _scheduler.ScheduleAsync(new ScheduledReminder(7, Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(3), "Za godzinę", "ZL-2026-0042"));

        _session.End(SessionEndReason.Expired);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        _scheduler.Scheduled.ShouldHaveSingleItem();
    }

    public void Dispose() => _coordinator.Dispose();

    private ReminderReconciler CreateReconciler()
    {
        Interlocked.Increment(ref _reconcileRequests);
        throw new InvalidOperationException("The database is closed.");
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
