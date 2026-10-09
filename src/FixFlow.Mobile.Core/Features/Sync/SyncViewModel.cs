using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Accessibility;
using FixFlow.Mobile.Core.Clipboard;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Dispatching;
using FixFlow.Mobile.Core.Features.Sync.Conflicts;
using FixFlow.Mobile.Core.Features.Sync.Outbox;
using FixFlow.Mobile.Core.Features.Sync.Pull;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.Core.Screens;
using FixFlow.Mobile.Core.Storage;

namespace FixFlow.Mobile.Core.Features.Sync;

public sealed partial class SyncViewModel
    : ObservableObject, IRecipient<ConnectivityChangedMessage>, IRecipient<SyncCompletedMessage>, IRecipient<ServerStartingMessage>
{
    private readonly UserDatabase _userDatabase;
    private readonly OutboxRepository _outbox;
    private readonly WorkOrderRepository _workOrders;
    private readonly SyncStateRepository _syncStates;
    private readonly SyncCoordinator _coordinator;
    private readonly ConflictResolver _resolver;
    private readonly SyncPresenter _presenter;
    private readonly DisplayFormatter _formatter;
    private readonly IConnectivityService _connectivity;
    private readonly IClipboardService _clipboard;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _ui;
    private readonly IScreenReaderAnnouncer _announcer;
    private readonly IMessenger _messenger;
    private int _localVersion;

    public SyncViewModel(
        ScreenState screen,
        UserDatabase userDatabase,
        OutboxRepository outbox,
        WorkOrderRepository workOrders,
        SyncStateRepository syncStates,
        SyncCoordinator coordinator,
        ConflictResolver resolver,
        SyncPresenter presenter,
        DisplayFormatter formatter,
        IConnectivityService connectivity,
        IClipboardService clipboard,
        IDialogService dialogs,
        IUiDispatcher ui,
        IScreenReaderAnnouncer announcer,
        IMessenger messenger)
    {
        Screen = screen;
        _userDatabase = userDatabase;
        _outbox = outbox;
        _workOrders = workOrders;
        _syncStates = syncStates;
        _coordinator = coordinator;
        _resolver = resolver;
        _presenter = presenter;
        _formatter = formatter;
        _connectivity = connectivity;
        _clipboard = clipboard;
        _dialogs = dialogs;
        _ui = ui;
        _announcer = announcer;
        _messenger = messenger;
    }

    public ScreenState Screen { get; }

    public void Resume()
    {
        if (!_messenger.IsRegistered<SyncCompletedMessage>(this))
        {
            _messenger.RegisterAll(this);
        }
    }

    public void Pause() => _messenger.UnregisterAll(this);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasConflicts))]
    public partial IReadOnlyList<ConflictView> Conflicts { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingOperations))]
    public partial IReadOnlyList<PendingOperationView> PendingOperations { get; private set; } = [];

    [ObservableProperty]
    public partial string? LastSyncText { get; private set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    public bool HasConflicts => Conflicts.Count > 0;

    public bool HasPendingOperations => PendingOperations.Count > 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await ShowLocalAsync();
        await ShowConnectivityAsync();
    }

    [RelayCommand]
    private async Task SynchronizeAsync()
    {
        try
        {
            Screen.ClearNotice();
            if (!await ShowConnectivityAsync())
            {
                return;
            }

            var result = await _coordinator.SynchronizeAsync(refreshPartCatalog: true);
            if (result.IsServerUnavailable || result.Pull?.Outcome == PullOutcome.Failed)
            {
                Screen.ShowNotice(ScreenNotice.Problem(AppResources.SyncFailedNotice));
                await ShowLocalAsync();
                return;
            }

            await ShowLocalAsync();
            _announcer.Announce(SyncPresenter.DescribeCompletion(PendingOperations.Count, Conflicts.Count));
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task CopyAsync(ConflictView conflict)
    {
        await _clipboard.SetTextAsync(conflict.Content);
        Screen.ShowNotice(ScreenNotice.Info(AppResources.ChangeCopied));
    }

    [RelayCommand]
    private async Task RetryAsync(ConflictView conflict)
    {
        Screen.ClearNotice();
        await _resolver.RetryAsync(conflict.OperationId);
        await ShowLocalAsync();
    }

    [RelayCommand]
    private async Task DiscardAsync(ConflictView conflict)
    {
        if (!await _dialogs.ConfirmAsync(
                AppResources.DiscardChangeTitle,
                AppResources.DiscardChangeMessage,
                AppResources.DiscardChangeButton,
                AppResources.CancelButton))
        {
            return;
        }

        Screen.ClearNotice();
        await _resolver.DiscardAsync(conflict.OperationId);
        await ShowLocalAsync();
    }

    [RelayCommand]
    private async Task DiscardAllAsync(ConflictView conflict)
    {
        if (!await _dialogs.ConfirmAsync(
                AppResources.DiscardAllChangesTitle,
                AppText.Format(AppResources.DiscardAllChangesMessage, conflict.FollowingChangeCount + 1),
                AppResources.DiscardChangeButton,
                AppResources.CancelButton))
        {
            return;
        }

        Screen.ClearNotice();
        await _resolver.DiscardWorkOrderChangesAsync(conflict.WorkOrderId);
        await ShowLocalAsync();
    }

    public void Receive(SyncCompletedMessage message) => _ui.Post(() => IsSignedIn ? LoadAsync() : Task.CompletedTask);

    public void Receive(ServerStartingMessage message) =>
        _ui.Post(() =>
        {
            Screen.ShowNotice(ScreenNotice.Info(AppResources.ServerStartingNotice));
            return Task.CompletedTask;
        });

    public void Receive(ConnectivityChangedMessage message) => _ui.Post(() => IsSignedIn ? ShowConnectivityAsync() : Task.CompletedTask);

    private bool IsSignedIn => _userDatabase.UserId is not null;

    private async Task<bool> ShowConnectivityAsync()
    {
        var lastSyncedAt = (await _syncStates.GetAsync(WorkOrderSyncService.SyncStateKey))?.UpdatedAt;
        LastSyncText = lastSyncedAt is { } syncedAt
            ? AppText.Format(AppResources.SyncLastSync, _formatter.FormatDateTime(syncedAt))
            : AppResources.SyncNeverSynced;
        if (_connectivity.IsOnline)
        {
            Screen.SetOnline();
            return true;
        }

        Screen.SetOffline(lastSyncedAt);
        return false;
    }

    private async Task ShowLocalAsync()
    {
        var version = ++_localVersion;
        var conflicts = await _outbox.GetConflictsAsync();
        var operations = await _outbox.GetAllAsync();
        var numbers = (await _workOrders.GetAllAsync()).ToDictionary(workOrder => workOrder.Id, workOrder => workOrder.Number);
        if (version != _localVersion)
        {
            return;
        }

        Conflicts = _presenter.PresentConflicts(conflicts, operations, numbers);
        PendingOperations = _presenter.PresentPending(operations, conflicts, numbers);
        Screen.ShowContent(hasItems: Conflicts.Count > 0 || PendingOperations.Count > 0);
    }
}
