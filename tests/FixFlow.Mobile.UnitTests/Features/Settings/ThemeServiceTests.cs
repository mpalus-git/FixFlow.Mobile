using FixFlow.Mobile.Core.Features.Settings;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.Settings;

public sealed class ThemeServiceTests
{
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly ThemeService _service;

    public ThemeServiceTests()
    {
        _settingsStore.GetString(Arg.Any<string>()).Returns((string?)null);
        _service = new ThemeService(_settingsStore);
    }

    [Fact]
    public void Should_Follow_System_When_No_Theme_Is_Saved()
    {
        _service.ApplySavedTheme();

        _service.CurrentTheme.ShouldBe(AppThemePreference.System);
    }

    [Theory]
    [InlineData("light", AppThemePreference.Light)]
    [InlineData("dark", AppThemePreference.Dark)]
    [InlineData("unknown", AppThemePreference.System)]
    public void Should_Use_Saved_Theme_When_Theme_Was_Saved(string saved, AppThemePreference expected)
    {
        _settingsStore.GetString(ThemeService.ThemeKey).Returns(saved);

        _service.ApplySavedTheme();

        _service.CurrentTheme.ShouldBe(expected);
    }

    [Fact]
    public void Should_Save_Theme_And_Raise_Event_When_Theme_Changes()
    {
        _service.ApplySavedTheme();
        var raised = 0;
        _service.ThemeChanged += (_, _) => raised++;

        _service.ChangeTheme(AppThemePreference.Dark);

        _settingsStore.Received(1).SetString(ThemeService.ThemeKey, "dark");
        _service.CurrentTheme.ShouldBe(AppThemePreference.Dark);
        raised.ShouldBe(1);
    }

    [Fact]
    public void Should_Not_Save_Or_Raise_Event_When_Same_Theme_Is_Selected()
    {
        _service.ApplySavedTheme();
        var raised = 0;
        _service.ThemeChanged += (_, _) => raised++;

        _service.ChangeTheme(AppThemePreference.System);

        _settingsStore.DidNotReceiveWithAnyArgs().SetString(default!, default!);
        raised.ShouldBe(0);
    }
}
