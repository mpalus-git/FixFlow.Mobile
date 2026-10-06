using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Notifications;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class NotificationConsentTests
{
    private readonly ISettingsStore _settings = Substitute.For<ISettingsStore>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    public NotificationConsentTests()
    {
        _settings.GetString(Arg.Any<string>()).Returns((string?)null);
    }

    [Fact]
    public async Task Should_Allow_Without_Explanation_When_Notifications_Are_Already_Enabled()
    {
        var scheduler = new FakeNotificationScheduler { IsEnabled = true };

        (await new NotificationConsent(scheduler, _settings, _dialogs).EnsureAllowedAsync()).ShouldBeTrue();

        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task Should_Explain_Before_System_Request_When_Asked_For_First_Time()
    {
        var scheduler = new FakeNotificationScheduler { IsEnabled = false, GrantsPermission = true };
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);

        (await new NotificationConsent(scheduler, _settings, _dialogs).EnsureAllowedAsync()).ShouldBeTrue();

        scheduler.PermissionRequests.ShouldBe(1);
        _settings.Received(1).SetString(NotificationConsent.AskedKey, Arg.Any<string>());
    }

    [Fact]
    public async Task Should_Not_Show_System_Request_When_Technician_Declines_Explanation()
    {
        var scheduler = new FakeNotificationScheduler { IsEnabled = false };
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);

        (await new NotificationConsent(scheduler, _settings, _dialogs).EnsureAllowedAsync()).ShouldBeFalse();

        scheduler.PermissionRequests.ShouldBe(0);
    }

    [Fact]
    public async Task Should_Not_Ask_Again_When_Technician_Was_Already_Asked()
    {
        var scheduler = new FakeNotificationScheduler { IsEnabled = false };
        _settings.GetString(NotificationConsent.AskedKey).Returns(bool.TrueString);

        (await new NotificationConsent(scheduler, _settings, _dialogs).EnsureAllowedAsync()).ShouldBeFalse();

        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
        scheduler.PermissionRequests.ShouldBe(0);
    }
}
