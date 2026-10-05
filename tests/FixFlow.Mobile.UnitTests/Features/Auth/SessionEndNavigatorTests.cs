using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Resources;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.Auth;

public sealed class SessionEndNavigatorTests
{
    private readonly SessionStore _session = new(new InMemorySecureStore(), TimeProvider.System);
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    public SessionEndNavigatorTests()
    {
        new SessionEndNavigator(_session, _navigation, _dialogs).Start();
    }

    [Fact]
    public async Task Should_Show_Login_With_Expiry_Notice_When_Session_Expires()
    {
        _session.End(SessionEndReason.Expired);

        await _navigation.Received(1).NavigateToRootAsync<LoginViewModel>();
        await _dialogs.Received(1).ShowAlertAsync(AppResources.SessionExpiredTitle, AppResources.SessionExpiredMessage, AppResources.CloseButton);
    }

    [Fact]
    public async Task Should_Show_Login_Without_Notice_When_User_Signs_Out()
    {
        _session.End(SessionEndReason.SignedOut);

        await _navigation.Received(1).NavigateToRootAsync<LoginViewModel>();
        await _dialogs.DidNotReceiveWithAnyArgs().ShowAlertAsync(default!, default!, default!);
    }
}
