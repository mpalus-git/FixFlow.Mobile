using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Dispatching;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Launching;
using FixFlow.Mobile.Core.Location;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.Core.Screens;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

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

    public ScreenState Screen { get; } = screen;

    [ObservableProperty]
    public partial WorkOrderDetailsView? Details { get; private set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial string? Notice { get; private set; }

    public bool HasNotice => Notice is not null;

    [ObservableProperty]
    public partial bool CanStartWork { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStartBlockedText))]
    public partial string? StartBlockedText { get; private set; }

    public bool HasStartBlockedText => StartBlockedText is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWorkTimer))]
    public partial WorkTimerView? WorkTimer { get; private set; }

    public bool HasWorkTimer => WorkTimer is not null;

    [ObservableProperty]
    public partial bool CanAddEntry { get; private set; }

    [ObservableProperty]
    public partial bool CanAddCorrection { get; private set; }

    [ObservableProperty]
    public partial bool CanComplete { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCompleteBlockedText))]
    public partial string? CompleteBlockedText { get; private set; }

    public bool HasCompleteBlockedText => CompleteBlockedText is not null;

    [ObservableProperty]
    public partial bool HasRejectedChanges { get; private set; }

    public void Resume()
    {
        _clock ??= timeProvider.CreateTimer(_ => uiDispatcher.Post(ShowWorkTimerAsync), null, ClockInterval, ClockInterval);
        if (!messenger.IsRegistered<SyncCompletedMessage>(this))
        {
            messenger.Register<WorkOrderDetailsViewModel, SyncCompletedMessage>(this, static (recipient, _) => recipient.ReloadLocal());
        }

        ReloadLocal();
    }

    public void Pause()
    {
        _clock?.Dispose();
        _clock = null;
        messenger.Unregister<SyncCompletedMessage>(this);
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
            Notice = null;
            if (!connectivity.IsOnline)
            {
                Screen.SetOffline((await syncStates.GetAsync(WorkOrderSyncService.SyncStateKey))?.UpdatedAt);
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
                    CanStartWork = false;
                    CanAddEntry = false;
                    CanAddCorrection = false;
                    CanComplete = false;
                    CompleteBlockedText = null;
                    StartBlockedText = null;
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
        if (Details is not { } details || !CanStartWork
            || !await dialogs.ConfirmAsync(
                AppResources.StartWorkConfirmTitle,
                WorkOrderTexts.Format(AppResources.StartWorkConfirmMessage, details.Number),
                AppResources.StartWorkButton,
                AppResources.CancelButton))
        {
            return;
        }

        await ShowLocalAsync();
        if (!CanStartWork)
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
        if (Details is not { } details || !CanComplete
            || !await dialogs.ConfirmAsync(
                AppResources.CompleteConfirmTitle,
                WorkOrderTexts.Format(AppResources.CompleteConfirmMessage, details.Number),
                AppResources.CompleteWorkOrderButton,
                AppResources.CancelButton))
        {
            return;
        }

        await outboxWriter.CompleteWorkOrderAsync(_workOrderId);
        await ShowLocalAsync();
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
            uiDispatcher.Post(ShowLocalAsync);
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
        var availability = WorkOrderStartPolicy.Evaluate(workOrder, PendingOperationsOverlay.Apply(await sources.WorkOrders.GetAllAsync(), allOperations));
        var workTimer = workOrder.Status == WorkOrderStatus.InProgress ? await sources.WorkTimers.GetAsync(_workOrderId) : null;
        var storedEntries = await sources.ServiceEntries.GetForWorkOrderAsync(workOrder.Id);
        var pendingEntries = PendingOperationsOverlay.PendingEntries(pendingOperations);
        var details = presenter.Present(
            workOrder,
            await sources.Clients.GetAsync(workOrder.ClientId),
            await sources.Devices.GetAsync(workOrder.DeviceId),
            storedEntries,
            await sources.DeviceHistory.GetForDeviceAsync(workOrder.DeviceId),
            pendingOperations,
            photos.ExistingPathFor);
        var hasRejectedChanges = (await sources.Outbox.GetConflictsAsync()).Any(conflict => conflict.WorkOrderId == _workOrderId);
        if (version != _localVersion)
        {
            return;
        }

        CanStartWork = availability.CanStart;
        StartBlockedText = availability.BlockingWorkOrderNumber is { } number ? WorkOrderTexts.Format(AppResources.StartWorkBlocked, number) : null;
        _workTimer = workTimer;
        await ShowWorkTimerAsync();
        Details = details;
        HasRejectedChanges = hasRejectedChanges;
        var inProgress = workOrder.Status == WorkOrderStatus.InProgress;
        var hasEntries = storedEntries.Count > 0 || pendingEntries.Count > 0;
        CanAddEntry = inProgress;
        CanAddCorrection = inProgress && NetPartUsage.Calculate(storedEntries, pendingEntries).Count > 0;
        CanComplete = inProgress && hasEntries;
        CompleteBlockedText = inProgress && !hasEntries ? AppResources.CompleteRequiresEntry : null;
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
            Notice = AppResources.DetailsRefreshFailed;
        }
        else
        {
            Screen.ShowError(AppResources.DetailsLoadFailed);
        }
    }
}
