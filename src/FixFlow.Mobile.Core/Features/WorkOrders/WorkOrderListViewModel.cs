using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Screens;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed partial class WorkOrderListViewModel(ScreenState screen, AuthService authService) : ObservableObject
{
    public ScreenState Screen { get; } = screen;

    [RelayCommand]
    private Task RefreshAsync()
    {
        Screen.StartLoading();
        Screen.ShowContent(hasItems: false);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task LogoutAsync() => authService.LogoutAsync();
}
