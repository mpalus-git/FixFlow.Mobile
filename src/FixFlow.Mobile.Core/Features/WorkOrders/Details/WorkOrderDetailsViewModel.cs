using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Dispatching;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync.Outbox;
using FixFlow.Mobile.Core.Features.Sync.Pull;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders.WorkTime;
using FixFlow.Mobile.Core.Launching;
using FixFlow.Mobile.Core.Location;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.Core.Screens;

namespace FixFlow.Mobile.Core.Features.WorkOrders.Details;

public sealed partial class WorkOrderDetailsViewModel(
    ScreenState screen,
    WorkOrderDetailsSources sources,
    WorkOrderDetailsSyncService sync,
    SyncStateRepository syncStates,
    WorkOrderDetailsPresenter presenter,
    ServiceProtocolService protocols,
    OutboxWriter outboxWriter,
    WorkLocationRecorder locationRecorder,
    ILocationService location,
    TimeProvider timeProvider,
    IUiDispatcher uiDispatcher,
    IMessenger messenger,
    PhotoStore photos,
    IDocumentLauncher documents,
    IConnectivityService connectivity,
    IMapLauncher maps,
    IUriLauncher uris,
    IDialogService dialogs,
    INavigationService navigation) : ObservableObject, INavigatedTo<Guid>
{
    public static readonly TimeSpan ClockInterval = TimeSpan.FromSeconds(30);

    private Guid _workOrderId;
    private WorkTimer? _workTimer;
    private bool _isLocating;
    private ITimer? _clock;
    private int _localVersion;
    private int _connectionVersion;
    private bool _hasPendingOperations;

    public ScreenState Screen { get; } = screen;

    [ObservableProperty]
    public partial WorkOrderDetailsView? Details { get; private set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial WorkOrderActions Actions { get; private set; } = WorkOrderActions.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWorkTimer))]
    public partial WorkTimerView? WorkTimer { get; private set; }

    public bool HasWorkTimer => WorkTimer is not null;

    [ObservableProperty]
    public partial bool HasRejectedChanges { get; private set; }

    public void Resume()
    {
        _clock ??= timeProvider.CreateTimer(_ => uiDispatcher.Post(ShowWorkTimerAsync), null, ClockInterval, ClockInterval);
        if (!messenger.IsRegistered<SyncCompletedMessage>(this))
        {
            messenger.Register<WorkOrderDetailsViewModel, SyncCompletedMessage>(this, static (recipient, message) => recipient.OnSyncCompleted(message.Result));
            messenger.Register<WorkOrderDetailsViewModel, ConnectivityChangedMessage>(this, static (recipient, message) => recipient.OnConnectivityChanged(message.IsOnline));
        }

        ReloadLocal();
    }

    public void Pause()
    {
        _clock?.Dispose();
        _clock = null;
        messenger.Unregister<SyncCompletedMessage>(this);
        messenger.Unregister<ConnectivityChangedMessage>(this);
    }

    public async Task OnNavigatedToAsync(Guid parameter)
    {
        _workOrderId = parameter;
        await LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (Details is null)
        {
            Screen.StartLoading();
        }

        await ShowLocalAsync();
        await RefreshAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            Screen.ClearNotice();
            if (!connectivity.IsOnline)
            {
                await ShowConnectionAsync(isOnline: false);
                if (Details is null)
                {
                    Screen.ShowError(AppResources.DetailsLoadFailed);
                }

                return;
            }

            Screen.SetOnline();
            var result = await sync.PullAsync(_workOrderId, cancellationToken);
            switch (result.Outcome)
            {
                case PullOutcome.Succeeded:
                    await ShowLocalAsync();
                    break;
                case PullOutcome.NotFound:
                    Details = null;
                    Actions = WorkOrderActions.None;
                    WorkTimer = null;
                    Screen.ShowError(AppResources.WorkOrderNoLongerAssigned);
                    break;
                default:
                    await ShowLocalAsync();
                    ShowRefreshFailure(result.Error);
                    break;
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task StartWorkAsync(CancellationToken cancellationToken)
    {
        if (Details is not { } details || !Actions.CanStartWork
            || !await dialogs.ConfirmAsync(
                AppResources.StartWorkConfirmTitle,
                AppText.Format(AppResources.StartWorkConfirmMessage, details.Number),
                AppResources.StartWorkButton,
                AppResources.CancelButton))
        {
            return;
        }

        await ShowLocalAsync();
        if (!Actions.CanStartWork)
        {
            return;
        }

        await outboxWriter.StartWorkAsync(_workOrderId);
        await ShowLocalAsync();
        await RecordLocationAsync(cancellationToken);
    }

    [RelayCommand]
    private Task AddCorrectionAsync() => navigation.NavigateToAsync<CorrectionEntryViewModel, Guid>(_workOrderId);

    [RelayCommand]
    private async Task CompleteAsync()
    {
        if (Actions.CanComplete)
        {
            await navigation.NavigateToAsync<CompleteWorkOrderViewModel, Guid>(_workOrderId);
        }
    }

    [RelayCommand]
    private Task OpenSyncAsync() => navigation.NavigateToAsync<SyncViewModel>();

    [RelayCommand]
    private Task AddEntryAsync() => navigation.NavigateToAsync<ServiceEntryViewModel, Guid>(_workOrderId);

    [RelayCommand]
    private Task RetryLocationAsync(CancellationToken cancellationToken) => RecordLocationAsync(cancellationToken);

    [RelayCommand]
    private Task OpenLocationSettingsAsync() => location.OpenSettingsAsync();

    [RelayCommand]
    private async Task CallClientAsync()
    {
        if (Details?.Client is { } client
            && !await uris.OpenAsync(new Uri("tel:" + new string([.. client.Phone.Where(character => char.IsDigit(character) || character == '+')]))))
        {
            await ShowErrorAsync(AppResources.PhoneUnavailable);
        }
    }

    [RelayCommand]
    private async Task OpenAddressAsync()
    {
        if (Details?.Client is { } client && !await maps.OpenAddressAsync(client.Name, client.Street, client.PostalCode, client.City))
        {
            await ShowErrorAsync(AppResources.MapUnavailable);
        }
    }

    [RelayCommand]
    private async Task OpenEntryLocationAsync(ServiceEntryView entry)
    {
        if (entry is { Latitude: { } latitude, Longitude: { } longitude }
            && !await maps.OpenLocationAsync(Details?.Number ?? string.Empty, latitude, longitude))
        {
            await ShowErrorAsync(AppResources.MapUnavailable);
        }
    }

    [RelayCommand]
    private async Task DownloadProtocolAsync(CancellationToken cancellationToken)
    {
        if (Details is not { CanDownloadProtocol: true } details)
        {
            return;
        }

        if (!connectivity.IsOnline)
        {
            await ShowErrorAsync(AppResources.ProtocolRequiresConnection);
            return;
        }

        if (await protocols.OpenAsync(_workOrderId, details.Number, cancellationToken) is { } error)
        {
            await ShowErrorAsync(error.Detail ?? AppResources.ProtocolFailed);
        }
    }

    [RelayCommand]
    private async Task OpenPhotoAsync(EntryPhotoView photo)
    {
        if (photo.IsLocal)
        {
            await using var content = File.OpenRead(photo.Source);
            await documents.OpenAsync(content, Path.GetFileName(photo.Source), PhotoUploadClient.JpegMediaType);
        }
        else if (!Uri.TryCreate(photo.Source, UriKind.Absolute, out var uri) || !await uris.OpenAsync(uri))
        {
            await ShowErrorAsync(AppResources.PhotoUnavailable);
        }
    }

    [RelayCommand]
    private Task OpenHistoryItemAsync(DeviceHistoryView item) =>
        navigation.NavigateToAsync<WorkOrderDetailsViewModel, Guid>(item.WorkOrderId);

    private async Task RecordLocationAsync(CancellationToken cancellationToken)
    {
        _isLocating = true;
        await ShowWorkTimerAsync();
        try
        {
            await locationRecorder.RecordAsync(_workOrderId, cancellationToken);
        }
        finally
        {
            _isLocating = false;
            _workTimer = await sources.WorkTimers.GetAsync(_workOrderId);
            await ShowWorkTimerAsync();
        }
    }

    private void ReloadLocal()
    {
        if (Details is not null)
        {
            uiDispatcher.Post(async () =>
            {
                await ShowConnectionAsync(connectivity.IsOnline);
                await ShowLocalAsync();
            });
        }
    }

    private void OnSyncCompleted(SyncResult result)
    {
        if (Details is not null)
        {
            uiDispatcher.Post(() => ApplySyncResultAsync(result));
        }
    }

    private async Task ApplySyncResultAsync(SyncResult result)
    {
        await ShowConnectionAsync(!result.IsOffline);
        if (result.Pull?.Outcome == PullOutcome.Succeeded && (Screen.Notice is not null || (_hasPendingOperations && result.Dispatch?.Sent > 0)))
        {
            await RefreshCommand.ExecuteAsync(null);
            return;
        }

        await ShowLocalAsync();
    }

    private void OnConnectivityChanged(bool isOnline) => uiDispatcher.Post(() => ShowConnectionAsync(isOnline));

    private async Task ShowConnectionAsync(bool isOnline)
    {
        var version = ++_connectionVersion;
        if (isOnline)
        {
            Screen.SetOnline();
            return;
        }

        var lastSyncedAt = (await syncStates.GetAsync(WorkOrderSyncService.SyncStateKey))?.UpdatedAt;
        if (version == _connectionVersion)
        {
            Screen.SetOffline(lastSyncedAt);
        }
    }

    private Task ShowWorkTimerAsync()
    {
        WorkTimer = _workTimer is null ? null : presenter.PresentWorkTimer(_workTimer, timeProvider.GetUtcNow(), _isLocating);
        return Task.CompletedTask;
    }

    private Task ShowErrorAsync(string message) =>
        dialogs.ShowAlertAsync(AppResources.ErrorTitle, message, AppResources.CloseButton);

    private async Task ShowLocalAsync()
    {
        var version = ++_localVersion;
        if (await sources.WorkOrders.GetAsync(_workOrderId) is not { } storedWorkOrder)
        {
            return;
        }

        var allOperations = await sources.Outbox.GetAllAsync();
        var pendingOperations = allOperations.Where(operation => operation.WorkOrderId == _workOrderId).ToList();
        var workOrder = PendingOperationsOverlay.Apply(storedWorkOrder, pendingOperations);
        var technicianWorkOrders = PendingOperationsOverlay.Apply(await sources.WorkOrders.GetAllAsync(), allOperations);
        var workTimer = workOrder.Status == WorkOrderStatus.InProgress ? await sources.WorkTimers.GetAsync(_workOrderId) : null;
        var storedEntries = await sources.ServiceEntries.GetForWorkOrderAsync(workOrder.Id);
        var pendingEntries = PendingOperationsOverlay.PendingEntries(pendingOperations);
        var details = presenter.Present(
            workOrder,
            await sources.Devices.GetAsync(workOrder.DeviceId),
            storedEntries,
            await sources.DeviceHistory.GetForDeviceAsync(workOrder.DeviceId),
            await sources.WorkOrderEvents.GetForWorkOrderAsync(workOrder.Id),
            pendingOperations,
            photos.ExistingPathFor);
        var hasRejectedChanges = (await sources.Outbox.GetConflictsAsync()).Any(conflict => conflict.WorkOrderId == _workOrderId);
        if (version != _localVersion)
        {
            return;
        }

        Actions = WorkOrderActions.Evaluate(workOrder, technicianWorkOrders, storedEntries, pendingEntries);
        _workTimer = workTimer;
        await ShowWorkTimerAsync();
        Details = details;
        _hasPendingOperations = pendingOperations.Count > 0;
        HasRejectedChanges = hasRejectedChanges;
        Screen.ShowContent(hasItems: true);
    }

    private void ShowRefreshFailure(ApiError? error)
    {
        if (error?.Kind == ApiErrorKind.Unauthorized)
        {
            return;
        }

        if (Details is not null)
        {
            Screen.ShowNotice(ScreenNotice.Problem(AppResources.DetailsRefreshFailed));
        }
        else
        {
            Screen.ShowError(AppResources.DetailsLoadFailed);
        }
    }
}
