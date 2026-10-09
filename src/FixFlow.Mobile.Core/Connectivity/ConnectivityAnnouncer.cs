using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Accessibility;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.Core.Storage;

namespace FixFlow.Mobile.Core.Connectivity;

public sealed class ConnectivityAnnouncer(
    UserDatabase userDatabase,
    IConnectivityService connectivity,
    IScreenReaderAnnouncer announcer,
    IMessenger messenger) : IRecipient<ConnectivityChangedMessage>
{
    private bool _isOnline;

    public void Start()
    {
        _isOnline = connectivity.IsOnline;
        messenger.RegisterAll(this);
    }

    public void Receive(ConnectivityChangedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.IsOnline == _isOnline)
        {
            return;
        }

        _isOnline = message.IsOnline;
        if (userDatabase.UserId is not null)
        {
            announcer.Announce(message.IsOnline ? AppResources.ConnectionRestoredAnnouncement : AppResources.ConnectionLostAnnouncement);
        }
    }
}
