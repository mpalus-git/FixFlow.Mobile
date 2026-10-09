using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Features.Sync.Outbox;
using FixFlow.Mobile.Core.Features.Sync.Pull;
using FixFlow.Mobile.Core.Storage;

namespace FixFlow.Mobile.Core.Features.Sync;

public sealed class SyncCoordinator : IRecipient<ConnectivityChangedMessage>, IRecipient<LocalChangeQueuedMessage>
{
    public static readonly TimeSpan PartCatalogMaxAge = TimeSpan.FromMinutes(15);

    private readonly Lock _gate = new();
    private readonly UserDatabase _userDatabase;
    private readonly IConnectivityService _connectivity;
    private readonly ServerWarmup _warmup;
    private readonly Func<ServerReadinessProbe> _readinessProbe;
    private readonly Func<OutboxDispatcher> _dispatcher;
    private readonly Func<WorkOrderSyncService> _workOrderSync;
    private readonly Func<PartSyncService> _partSync;
    private readonly IMessenger _messenger;
    private readonly TimeProvider _timeProvider;
    private Task<SyncResult>? _running;
    private bool _rerunRequested;
    private ITimer? _retryTimer;
    private bool _retriesSuspended;
    private bool _partCatalogRequested;

    public SyncCoordinator(
        UserDatabase userDatabase,
        IConnectivityService connectivity,
        ServerWarmup warmup,
        Func<ServerReadinessProbe> readinessProbe,
        Func<OutboxDispatcher> dispatcher,
        Func<WorkOrderSyncService> workOrderSync,
        Func<PartSyncService> partSync,
        IMessenger messenger,
        TimeProvider timeProvider)
    {
        _userDatabase = userDatabase;
        _connectivity = connectivity;
        _warmup = warmup;
        _readinessProbe = readinessProbe;
        _dispatcher = dispatcher;
        _workOrderSync = workOrderSync;
        _partSync = partSync;
        _messenger = messenger;
        _timeProvider = timeProvider;
    }

    public void Start() => _messenger.RegisterAll(this);

    public Task<SyncResult> SynchronizeAsync(bool refreshPartCatalog = false)
    {
        lock (_gate)
        {
            _partCatalogRequested |= refreshPartCatalog;
            if (_running is { IsCompleted: false })
            {
                _rerunRequested = true;
                return _running;
            }

            _running = RunUntilSettledAsync();
            return _running;
        }
    }

    public void SuspendRetries()
    {
        lock (_gate)
        {
            _retriesSuspended = true;
            CancelRetry();
        }
    }

    public Task ResumeAsync()
    {
        lock (_gate)
        {
            _retriesSuspended = false;
        }

        return SynchronizeInBackgroundAsync();
    }

    public async void Receive(ConnectivityChangedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.IsOnline)
        {
            await SynchronizeInBackgroundAsync();
        }
    }

    public async void Receive(LocalChangeQueuedMessage message) => await SynchronizeInBackgroundAsync();

    public async Task SynchronizeInBackgroundAsync()
    {
        try
        {
            await SynchronizeAsync();
        }
        catch (Exception)
        {
        }
    }

    private async Task<SyncResult> RunUntilSettledAsync()
    {
        await Task.Yield();
        while (true)
        {
            var result = await RunOnceAsync();
            lock (_gate)
            {
                if (!_rerunRequested)
                {
                    return result;
                }

                _rerunRequested = false;
            }
        }
    }

    private async Task<SyncResult> RunOnceAsync()
    {
        bool refreshPartCatalog;
        lock (_gate)
        {
            refreshPartCatalog = _partCatalogRequested;
            _partCatalogRequested = false;
        }

        var result = await SynchronizeOnceAsync(refreshPartCatalog);
        ScheduleRetry(result.Dispatch);
        _messenger.Send(new SyncCompletedMessage(result));
        return result;
    }

    private void ScheduleRetry(DispatchSummary? dispatch)
    {
        lock (_gate)
        {
            CancelRetry();
            if (_retriesSuspended || dispatch is not { SessionRejected: false, NextAttemptAt: { } nextAttemptAt })
            {
                return;
            }

            var delay = nextAttemptAt - _timeProvider.GetUtcNow();
            _retryTimer = _timeProvider.CreateTimer(
                _ => _ = SynchronizeInBackgroundAsync(), null, delay > TimeSpan.Zero ? delay : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
    }

    private void CancelRetry()
    {
        _retryTimer?.Dispose();
        _retryTimer = null;
    }

    private async Task<SyncResult> SynchronizeOnceAsync(bool refreshPartCatalog)
    {
        if (_userDatabase.UserId is null)
        {
            return SyncResult.NoSession;
        }

        if (!_connectivity.IsOnline)
        {
            return SyncResult.Offline;
        }

        if (!_warmup.IsServerReady)
        {
            if (!await _readinessProbe().WaitUntilReadyAsync(() => _messenger.Send(new ServerStartingMessage()), CancellationToken.None))
            {
                return SyncResult.ServerUnavailable;
            }

            _warmup.MarkReady();
        }

        var dispatch = await _dispatcher().DispatchAsync(CancellationToken.None);
        if (dispatch.SessionRejected)
        {
            return new SyncResult(false, false, dispatch, null);
        }

        var pull = await _workOrderSync().PullAsync(CancellationToken.None);
        if (pull.Outcome == PullOutcome.Succeeded)
        {
            var partSync = _partSync();
            if (refreshPartCatalog || dispatch.ChangedPartStock || await partSync.GetLastPulledAtAsync() is not { } pulledAt || _timeProvider.GetUtcNow() - pulledAt >= PartCatalogMaxAge)
            {
                await partSync.PullAsync(CancellationToken.None);
            }
        }

        return new SyncResult(false, false, dispatch, pull);
    }
}
