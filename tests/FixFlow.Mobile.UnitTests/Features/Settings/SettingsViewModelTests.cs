using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.UnitTests.Features.Auth;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.Settings;

public sealed class SettingsViewModelTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);
    private static readonly SessionUser Technician = new(Guid.NewGuid(), "jan@fixflow.test", "Jan Kowalski", SessionUser.TechnicianRole);

    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly SessionStore _session;
    private readonly ILanguageService _language = Substitute.For<ILanguageService>();
    private readonly ISettingsStore _settings = Substitute.For<ISettingsStore>();
    private readonly IAppVersion _appVersion = Substitute.For<IAppVersion>();
    private readonly ThemeService _theme;

    public SettingsViewModelTests()
    {
        _session = new SessionStore(new InMemorySecureStore(), _timeProvider);
        _settings.GetString(Arg.Any<string>()).Returns((string?)null);
        _theme = new ThemeService(_settings);
        _language.CurrentLanguage.Returns(AppLanguage.Polish);
        _appVersion.Version.Returns("1.2");
        _appVersion.Build.Returns("34");
    }

    public async ValueTask InitializeAsync() =>
        await _session.StartAsync(
            new AuthTokensResponse("access-1", Now.AddMinutes(15), "refresh-1", Now.AddDays(7)),
            Technician);

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

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private SettingsViewModel CreateViewModel() =>
        new(_language, _theme, _session, _appVersion);
}
