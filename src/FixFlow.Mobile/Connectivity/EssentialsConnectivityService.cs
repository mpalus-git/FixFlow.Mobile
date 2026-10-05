using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Connectivity;

namespace FixFlow.Mobile.Connectivity;

public sealed class EssentialsConnectivityService : IConnectivityService
{
    private readonly IConnectivity _connectivity;
    private readonly IMessenger _messenger;

    public EssentialsConnectivityService(IConnectivity connectivity, IMessenger messenger)
    {
        _connectivity = connectivity;
        _messenger = messenger;
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    public bool IsOnline => _connectivity.NetworkAccess == NetworkAccess.Internet;

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() => _messenger.Send(new ConnectivityChangedMessage(IsOnline)));
}
