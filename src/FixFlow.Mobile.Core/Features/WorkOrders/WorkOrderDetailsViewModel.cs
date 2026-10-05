using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Launching;
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
    IConnectivityService connectivity,
    IMapLauncher maps,
    IUriLauncher uris,
    IDialogService dialogs,
    INavigationService navigation) : ObservableObject, INavigatedTo<Guid>
{
    private Guid _workOrderId;

    public ScreenState Screen { get; } = screen;

    [ObservableProperty]
    public partial WorkOrderDetailsView? Details { get; private set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial string? Notice { get; private set; }

    public bool HasNotice => Notice is not null;

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
    private Task OpenHistoryItemAsync(DeviceHistoryView item) =>
        navigation.NavigateToAsync<WorkOrderDetailsViewModel, Guid>(item.WorkOrderId);

    private Task ShowErrorAsync(string message) =>
        dialogs.ShowAlertAsync(AppResources.ErrorTitle, message, AppResources.CloseButton);

    private async Task ShowLocalAsync()
    {
        if (await sources.WorkOrders.GetAsync(_workOrderId) is not { } workOrder)
        {
            return;
        }

        Details = presenter.Present(
            workOrder,
            await sources.Clients.GetAsync(workOrder.ClientId),
            await sources.Devices.GetAsync(workOrder.DeviceId),
            await sources.ServiceEntries.GetForWorkOrderAsync(workOrder.Id),
            await sources.DeviceHistory.GetForDeviceAsync(workOrder.DeviceId));
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
