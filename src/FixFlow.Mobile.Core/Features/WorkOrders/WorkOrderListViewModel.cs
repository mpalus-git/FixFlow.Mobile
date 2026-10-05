using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.Core.Screens;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed partial class WorkOrderListViewModel(
    ScreenState screen,
    AuthService authService,
    WorkOrderRepository workOrders,
    WorkOrderSyncService sync,
    WorkOrderListPresenter presenter,
    IConnectivityService connectivity) : ObservableObject
{
    public ScreenState Screen { get; } = screen;

    [ObservableProperty]
    public partial IReadOnlyList<WorkOrderGroup> Groups { get; private set; } = [];

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial string? Notice { get; private set; }

    public bool HasNotice => Notice is not null;

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
            if (!connectivity.IsOnline)
            {
                Screen.SetOffline(await sync.GetLastPulledAtAsync());
                await ShowLocalAsync(updateScreen: true);
                return;
            }

            Screen.SetOnline();
            var result = await sync.PullAsync(() => Notice = AppResources.ServerStartingNotice, cancellationToken);
            Notice = null;
            if (result.Outcome == PullOutcome.Succeeded)
            {
                await ShowLocalAsync(updateScreen: true);
            }
            else if (result.Error?.Kind != ApiErrorKind.Unauthorized)
            {
                ShowRefreshFailure();
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private Task LogoutAsync() => authService.LogoutAsync();

    private async Task ShowLocalAsync(bool updateScreen)
    {
        Groups = presenter.Present(await workOrders.GetAllAsync());
        if (updateScreen)
        {
            Screen.ShowContent(hasItems: Groups.Count > 0);
        }
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
