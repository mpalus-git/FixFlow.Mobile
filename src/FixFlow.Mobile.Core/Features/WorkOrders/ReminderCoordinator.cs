using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Notifications;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed class ReminderCoordinator(
    SessionStore session,
    IMessenger messenger,
    Func<ReminderReconciler> reconciler,
    INotificationScheduler scheduler) : IRecipient<SyncCompletedMessage>, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public void Start()
    {
        messenger.RegisterAll(this);
        session.SessionEnded += OnSessionEnded;
    }

    public async void Receive(SyncCompletedMessage message) => await RunExclusiveAsync(() => reconciler().ReconcileAsync());

    public void Dispose() => _gate.Dispose();

    private async void OnSessionEnded(object? sender, SessionEndedEventArgs e)
    {
        if (e.Reason == SessionEndReason.SignedOut)
        {
            await RunExclusiveAsync(scheduler.CancelAllAsync);
        }
    }

    private async Task RunExclusiveAsync(Func<Task> action)
    {
        await _gate.WaitAsync();
        try
        {
            await action();
        }
        catch (Exception)
        {
        }
        finally
        {
            _gate.Release();
        }
    }
}
