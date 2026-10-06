using System.Net;
using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Notifications;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.UnitTests.Api;
using FixFlow.Mobile.UnitTests.Features.Auth;
using FixFlow.Mobile.UnitTests.Features.WorkOrders;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Refit;

namespace FixFlow.Mobile.UnitTests.Features.Settings;

public sealed class SettingsViewModelTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);
    private static readonly SessionUser Technician = new(Guid.NewGuid(), "jan@fixflow.test", "Jan Kowalski", SessionUser.TechnicianRole);

    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly TestUserDatabase _userDatabase = new();
    private readonly SessionStore _session;
    private readonly ILanguageService _language = Substitute.For<ILanguageService>();
    private readonly ISettingsStore _settings = Substitute.For<ISettingsStore>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IAppVersion _appVersion = Substitute.For<IAppVersion>();
    private readonly StubHttpMessageHandler _handler = new(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly OutboxRepository _outbox;
    private readonly ThemeService _theme;
    private FakeNotificationScheduler _scheduler = new() { IsEnabled = false };
    private int _reconcileRequests;

    public SettingsViewModelTests()
    {
        _session = new SessionStore(new InMemorySecureStore(), _timeProvider);
        _outbox = new OutboxRepository(_userDatabase.Database);
        _settings.GetString(Arg.Any<string>()).Returns((string?)null);
        _theme = new ThemeService(_settings);
        _language.CurrentLanguage.Returns(AppLanguage.Polish);
        _appVersion.Version.Returns("1.2");
        _appVersion.Build.Returns("34");
    }

    public async ValueTask InitializeAsync()
    {
        await _session.StartAsync(
            new AuthTokensResponse("access-1", Now.AddMinutes(15), "refresh-1", Now.AddDays(7)),
            Technician);
        await _userDatabase.Database.OpenAsync(Technician.Id);
    }

    [Fact]
    public void Should_Show_Signed_In_Technician_And_Version_When_Opened()
    {
        var viewModel = CreateViewModel();

        viewModel.UserName.ShouldBe("Jan Kowalski");
        viewModel.UserEmail.ShouldBe("jan@fixflow.test");
        viewModel.Version.ShouldBe(WorkOrderTexts.Format(AppResources.SettingsVersion, "1.2", "34"));
        viewModel.SelectedLanguage.ShouldBe(AppLanguage.Polish);
        viewModel.SelectedTheme.ShouldBe(AppThemePreference.System);
    }

    [Fact]
    public void Should_Change_Language_When_Other_Language_Is_Selected()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedLanguage = AppLanguage.English;

        _language.Received(1).ChangeLanguage(AppLanguage.English);
    }

    [Fact]
    public void Should_Save_Theme_When_Other_Theme_Is_Selected()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedTheme = AppThemePreference.Dark;

        _theme.CurrentTheme.ShouldBe(AppThemePreference.Dark);
        _settings.Received(1).SetString(ThemeService.ThemeKey, "dark");
    }

    [Fact]
    public async Task Should_Request_Permission_And_Plan_Reminders_When_Reminders_Are_Turned_On_First_Time()
    {
        var viewModel = CreateViewModel();
        await viewModel.RefreshNotificationsCommand.ExecuteAsync(null);
        viewModel.CanEnableNotifications.ShouldBeTrue();
        viewModel.EnableNotificationsText.ShouldBe(AppResources.EnableNotificationsButton);

        await viewModel.EnableNotificationsCommand.ExecuteAsync(null);

        _scheduler.PermissionRequests.ShouldBe(1);
        _settings.Received(1).SetString(NotificationConsent.AskedKey, Arg.Any<string>());
        viewModel.AreNotificationsEnabled.ShouldBeTrue();
        viewModel.CanEnableNotifications.ShouldBeFalse();
        viewModel.NotificationStatus.ShouldBe(AppResources.NotificationsEnabledStatus);
        _reconcileRequests.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Open_System_Settings_When_Permission_Was_Already_Requested()
    {
        _settings.GetString(NotificationConsent.AskedKey).Returns(bool.TrueString);
        var viewModel = CreateViewModel();
        await viewModel.RefreshNotificationsCommand.ExecuteAsync(null);
        viewModel.EnableNotificationsText.ShouldBe(AppResources.OpenNotificationSettingsButton);

        await viewModel.EnableNotificationsCommand.ExecuteAsync(null);

        _scheduler.SettingsOpenings.ShouldBe(1);
        _scheduler.PermissionRequests.ShouldBe(0);
        _reconcileRequests.ShouldBe(0);
    }

    [Fact]
    public async Task Should_Plan_Reminders_When_Notifications_Were_Turned_On_In_System_Settings()
    {
        var viewModel = CreateViewModel();
        await viewModel.RefreshNotificationsCommand.ExecuteAsync(null);

        _scheduler.IsEnabled = true;
        await viewModel.RefreshNotificationsCommand.ExecuteAsync(null);
        await viewModel.RefreshNotificationsCommand.ExecuteAsync(null);

        viewModel.AreNotificationsEnabled.ShouldBeTrue();
        _reconcileRequests.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Hide_Reminders_When_Platform_Does_Not_Support_Notifications()
    {
        _scheduler = new FakeNotificationScheduler { IsSupported = false, IsEnabled = false };
        var viewModel = CreateViewModel();

        await viewModel.RefreshNotificationsCommand.ExecuteAsync(null);

        viewModel.AreNotificationsSupported.ShouldBeFalse();
        viewModel.CanEnableNotifications.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Warn_About_Unsent_Changes_When_Logging_Out_With_Queue()
    {
        await QueueRejectedStartAndPendingCompletionAsync();
        var viewModel = CreateViewModel();

        await viewModel.LogoutCommand.ExecuteAsync(null);

        var expected = WorkOrderTexts.Format(
            AppResources.LogoutUnsentConfirmMessage,
            $"{WorkOrderTexts.Format(AppResources.LogoutPendingPart, 1)}, {WorkOrderTexts.Format(AppResources.LogoutConflictPart, 1)}");
        await _dialogs.Received(1).ConfirmAsync(AppResources.LogoutConfirmTitle, expected, AppResources.LogoutButton, AppResources.CancelButton);
    }

    [Fact]
    public async Task Should_Keep_Session_And_Data_When_Logout_Is_Cancelled()
    {
        await QueueRejectedStartAndPendingCompletionAsync();
        var viewModel = CreateViewModel();

        await viewModel.LogoutCommand.ExecuteAsync(null);

        _session.CurrentUser.ShouldBe(Technician);
        (await _outbox.CountPendingAsync()).ShouldBe(1);
        _handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Sign_Out_And_Delete_Local_Data_When_Logout_Is_Confirmed()
    {
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        var viewModel = CreateViewModel();
        SessionEndReason? endReason = null;
        _session.SessionEnded += (_, args) => endReason = args.Reason;

        await viewModel.LogoutCommand.ExecuteAsync(null);

        await _dialogs.Received(1).ConfirmAsync(AppResources.LogoutConfirmTitle, AppResources.LogoutConfirmMessage, AppResources.LogoutButton, AppResources.CancelButton);
        endReason.ShouldBe(SessionEndReason.SignedOut);
        File.Exists(_userDatabase.Database.PathFor(Technician.Id)).ShouldBeFalse();
    }

    public async ValueTask DisposeAsync()
    {
        _handler.Dispose();
        await _userDatabase.DisposeAsync();
    }

    private SettingsViewModel CreateViewModel()
    {
        var api = RestService.ForGenerated<IFixFlowApi>(new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") });
        var authService = new AuthService(api, api, _session, _userDatabase.Database, new PhotoStore(_userDatabase.Options, _userDatabase.Database), _timeProvider);
        return new SettingsViewModel(
            _language,
            _theme,
            _scheduler,
            _settings,
            new ReminderCoordinator(_session, _messenger, CreateReconciler, _scheduler),
            _outbox,
            authService,
            _session,
            _appVersion,
            _dialogs);
    }

    private ReminderReconciler CreateReconciler()
    {
        _reconcileRequests++;
        throw new InvalidOperationException("Reconciliation is not under test.");
    }

    private async Task QueueRejectedStartAndPendingCompletionAsync()
    {
        var writer = new OutboxWriter(_outbox, _timeProvider, _messenger);
        var workOrderId = Guid.NewGuid();
        var start = await writer.StartWorkAsync(workOrderId);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        await writer.CompleteWorkOrderAsync(workOrderId);
        await _outbox.MoveToConflictsAsync(new SyncConflict(
            start.Id, start.WorkOrderId, start.Kind, start.Payload, start.CreatedAt, HttpStatusCode.Conflict, "WorkOrder.TechnicianAlreadyHasWorkInProgress", null, Now));
    }
}
