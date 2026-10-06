using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Dispatching;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.Core.Screens;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed partial class WorkOrderListViewModel
    : ObservableObject, IRecipient<ConnectivityChangedMessage>, IRecipient<SyncCompletedMessage>, IRecipient<ServerStartingMessage>
{
    private readonly AuthService _authService;
    private readonly WorkOrderRepository _workOrders;
    private readonly OutboxRepository _outbox;
    private readonly WorkOrderSyncService _sync;
    private readonly SyncCoordinator _coordinator;
    private readonly IUiDispatcher _ui;
    private readonly WorkOrderListPresenter _presenter;
    private readonly IConnectivityService _connectivity;
    private readonly INavigationService _navigation;

    public WorkOrderListViewModel(
        ScreenState screen,
        AuthService authService,
        WorkOrderRepository workOrders,
        OutboxRepository outbox,
        WorkOrderSyncService sync,
        SyncCoordinator coordinator,
        WorkOrderListPresenter presenter,
        IConnectivityService connectivity,
        INavigationService navigation,
        IUiDispatcher ui,
        IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        Screen = screen;
        _authService = authService;
        _workOrders = workOrders;
        _outbox = outbox;
        _sync = sync;
        _coordinator = coordinator;
        _ui = ui;
        _presenter = presenter;
        _connectivity = connectivity;
        _navigation = navigation;
        messenger.RegisterAll(this);
    }

    public ScreenState Screen { get; }

    [ObservableProperty]
    public partial IReadOnlyList<WorkOrderGroup> Groups { get; private set; } = [];

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial string? Notice { get; private set; }

    [ObservableProperty]
    public partial WorkOrderListItem? SelectedWorkOrder { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSyncStatus))]
    public partial string? SyncStatus { get; private set; }

    public bool HasNotice => Notice is not null;

    public bool HasSyncStatus => SyncStatus is not null;

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
            Notice = null;
            if (!_connectivity.IsOnline)
            {
                Screen.SetOffline(await _sync.GetLastPulledAtAsync());
                await ShowLocalAsync(updateScreen: true);
                return;
            }

            Screen.SetOnline();
            var result = await _coordinator.SynchronizeAsync();
            Notice = null;
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
        _ui.Post(() => ShowLocalAsync(updateScreen: Groups.Count > 0));

    public void Receive(ServerStartingMessage message) =>
        _ui.Post(() =>
        {
            Notice = AppResources.ServerStartingNotice;
            return Task.CompletedTask;
        });

    public async void Receive(ConnectivityChangedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
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
    private Task LogoutAsync() => _authService.LogoutAsync();

    private async Task ShowLocalAsync(bool updateScreen)
    {
        var operations = await _outbox.GetAllAsync();
        var conflicts = await _outbox.GetConflictsAsync();
        SyncStatus = DescribeQueue(operations.Count, conflicts.Count);
        Groups = _presenter.Present(
            PendingOperationsOverlay.Apply(await _workOrders.GetAllAsync(), operations),
            operations.Select(operation => operation.WorkOrderId).ToHashSet(),
            conflicts.Select(conflict => conflict.WorkOrderId).ToHashSet());
        if (updateScreen)
        {
            Screen.ShowContent(hasItems: Groups.Count > 0);
        }
    }

    private static string? DescribeQueue(int pending, int conflicts)
    {
        string[] parts =
        [
            .. pending > 0 ? [WorkOrderTexts.Format(AppResources.SyncPendingCount, pending)] : Array.Empty<string>(),
            .. conflicts > 0 ? [WorkOrderTexts.Format(AppResources.SyncConflictCount, conflicts)] : Array.Empty<string>(),
        ];
        return parts.Length == 0 ? null : string.Join("  ·  ", parts);
    }

    private void ShowRefreshFailure()
    {
        if (Groups.Count > 0)
        {
            Notice = AppResources.RefreshFailedNotice;
        }
        else
        {
            Screen.ShowError(AppResources.WorkOrderListLoadFailed);
        }
    }
}
