using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Accessibility;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.UnitTests.Storage;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Connectivity;

public sealed class ConnectivityAnnouncerTests : IAsyncLifetime
{
    private readonly TestUserDatabase _userDatabase = new();
    private readonly IConnectivityService _connectivity = Substitute.For<IConnectivityService>();
    private readonly IScreenReaderAnnouncer _announcer = Substitute.For<IScreenReaderAnnouncer>();
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly ConnectivityAnnouncer _connectivityAnnouncer;

    public ConnectivityAnnouncerTests()
    {
        _connectivity.IsOnline.Returns(true);
        _connectivityAnnouncer = new ConnectivityAnnouncer(_userDatabase.Database, _connectivity, _announcer, _messenger);
        _connectivityAnnouncer.Start();
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public void Should_Announce_Lost_And_Restored_Connection_When_Technician_Is_Signed_In()
    {
        _messenger.Send(new ConnectivityChangedMessage(IsOnline: false));
        _messenger.Send(new ConnectivityChangedMessage(IsOnline: true));

        Received.InOrder(() =>
        {
            _announcer.Announce(AppResources.ConnectionLostAnnouncement);
            _announcer.Announce(AppResources.ConnectionRestoredAnnouncement);
        });
    }

    [Fact]
    public void Should_Announce_Once_When_Same_Connection_State_Is_Reported_Again()
    {
        _messenger.Send(new ConnectivityChangedMessage(IsOnline: true));
        _messenger.Send(new ConnectivityChangedMessage(IsOnline: false));
        _messenger.Send(new ConnectivityChangedMessage(IsOnline: false));

        _announcer.Received(1).Announce(Arg.Any<string>());
    }

    [Fact]
    public async Task Should_Stay_Silent_When_Technician_Signed_Out()
    {
        await _userDatabase.Database.CloseAsync();

        _messenger.Send(new ConnectivityChangedMessage(IsOnline: false));

        _announcer.DidNotReceiveWithAnyArgs().Announce(default!);
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();
}
