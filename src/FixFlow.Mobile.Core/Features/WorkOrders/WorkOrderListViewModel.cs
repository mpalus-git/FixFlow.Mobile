using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Dispatching;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.Core.Screens;
using FixFlow.Mobile.Core.Storage;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed partial class WorkOrderListViewModel
    : ObservableObject, IRecipient<ConnectivityChangedMessage>, IRecipient<SyncCompletedMessage>, IRecipient<ServerStartingMessage>
{
    public static readonly TimeSpan RefreshOnReturnAfter = TimeSpan.FromMinutes(5);

    private readonly UserDatabase _userDatabase;
    private readonly WorkOrderRepository _workOrders;
    private readonly OutboxRepository _outbox;
    private readonly WorkOrderSyncService _sync;
    private readonly SyncCoordinator _coordinator;
    private readonly IUiDispatcher _ui;
    private readonly WorkOrderListPresenter _presenter;
    private readonly IConnectivityService _connectivity;
    private readonly INavigationService _navigation;
    private readonly TimeProvider _timeProvider;
    private Guid? _loadedForUserId;

    public WorkOrderListViewModel(
        ScreenState screen,
        UserDatabase userDatabase,
        WorkOrderRepository workOrders,
        OutboxRepository outbox,
        WorkOrderSyncService sync,
        SyncCoordinator coordinator,
        WorkOrderListPresenter presenter,
        IConnectivityService connectivity,
        INavigationService navigation,
        IUiDispatcher ui,
        IMessenger messenger,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        Screen = screen;
        _userDatabase = userDatabase;
        _workOrders = workOrders;
        _outbox = outbox;
        _sync = sync;
        _coordinator = coordinator;
        _ui = ui;
        _presenter = presenter;
        _connectivity = connectivity;
        _navigation = navigation;
        _timeProvider = timeProvider;
        messenger.RegisterAll(this);
    }

    public ScreenState Screen { get; }

    [ObservableProperty]
    public partial IReadOnlyList<WorkOrderGroup> Groups { get; private set; } = [];

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial WorkOrderListItem? SelectedWorkOrder { get; set; }

    [RelayCommand]
    private async Task AppearAsync(CancellationToken cancellationToken)
    {
        if (_userDatabase.UserId is not { } userId)
        {
            return;
        }

        if (userId != _loadedForUserId)
        {
            _loadedForUserId = userId;
            await LoadAsync(cancellationToken);
        }
        else if (await _sync.GetLastPulledAtAsync() is { } pulledAt && _timeProvider.GetUtcNow() - pulledAt < RefreshOnReturnAfter)
        {
            await ShowLocalAsync(updateScreen: true);
        }
        else
        {
            await LoadAsync(cancellationToken);
        }
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (Groups.Count == 0)
        {
            Screen.StartLoading();
        }

        await ShowLocalAsync(updateScreen: false);
        if (Groups.Count > 0)
        {
            Screen.ShowContent(hasItems: true);
        }

        await RefreshAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            Screen.ClearNotice();
            if (!_connectivity.IsOnline)
            {
                Screen.SetOffline(await _sync.GetLastPulledAtAsync());
                await ShowLocalAsync(updateScreen: true);
                return;
            }

            Screen.SetOnline();
            var result = await _coordinator.SynchronizeAsync(refreshPartCatalog: true);
            Screen.ClearNotice();
            if (result.Pull?.Outcome == PullOutcome.Succeeded)
            {
                await ShowLocalAsync(updateScreen: true);
            }
            else if (result.IsServerUnavailable || (result.Pull is { } pull && pull.Error?.Kind != ApiErrorKind.Unauthorized))
            {
                await ShowLocalAsync(updateScreen: false);
                ShowRefreshFailure();
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private Task OpenSelectedWorkOrderAsync()
    {
        if (SelectedWorkOrder is not { } item)
        {
            return Task.CompletedTask;
        }

        SelectedWorkOrder = null;
        return _navigation.NavigateToAsync<WorkOrderDetailsViewModel, Guid>(item.Id);
    }

    public void Receive(SyncCompletedMessage message) =>
        _ui.Post(() => IsSignedIn ? ShowLocalAsync(updateScreen: Groups.Count > 0) : Task.CompletedTask);

    public void Receive(ServerStartingMessage message) =>
        _ui.Post(() =>
        {
            Screen.ShowNotice(ScreenNotice.Info(AppResources.ServerStartingNotice));
            return Task.CompletedTask;
        });

    public async void Receive(ConnectivityChangedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!IsSignedIn)
        {
            return;
        }

        if (message.IsOnline)
        {
            Screen.SetOnline();
        }
        else
        {
            Screen.SetOffline(await _sync.GetLastPulledAtAsync());
        }
    }

    [RelayCommand]
    private Task OpenSyncAsync() => _navigation.NavigateToAsync<SyncViewModel>();

    [RelayCommand]
    private Task OpenSettingsAsync() => _navigation.NavigateToAsync<SettingsViewModel>();

    private bool IsSignedIn => _userDatabase.UserId is not null;

    private async Task ShowLocalAsync(bool updateScreen)
    {
        var operations = await _outbox.GetAllAsync();
        var conflicts = await _outbox.GetConflictsAsync();
        Screen.SetQueue(operations.Count, conflicts.Count);
        var groups = _presenter.Present(
            PendingOperationsOverlay.Apply(await _workOrders.GetAllAsync(), operations),
            operations.Select(operation => operation.WorkOrderId).ToHashSet(),
            conflicts.Select(conflict => conflict.WorkOrderId).ToHashSet());
        if (!WorkOrderGroup.HaveSameContent(Groups, groups))
        {
            Groups = groups;
        }

        if (updateScreen)
        {
            Screen.ShowContent(hasItems: Groups.Count > 0);
        }
    }

    private void ShowRefreshFailure()
    {
        if (Groups.Count > 0)
        {
            Screen.ShowNotice(ScreenNotice.Problem(AppResources.RefreshFailedNotice));
        }
        else
        {
            Screen.ShowError(AppResources.WorkOrderListLoadFailed);
        }
    }
}
