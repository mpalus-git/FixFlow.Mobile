using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Formatting;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Formatting;

[Collection(GlobalCultureTests.Name)]
public sealed class DisplayFormatterTests : IDisposable
{
    private const char NoBreakSpace = ' ';

    private readonly GlobalCultureScope _cultureScope = new();
    private readonly LanguageService _languageService = new(Substitute.For<ISettingsStore>());
    private readonly DisplayFormatter _formatter;

    public DisplayFormatterTests()
    {
        _formatter = new DisplayFormatter(_languageService, BusinessClock.CreateForWarsaw(new FakeTimeProvider()));
    }

    [Theory]
    [InlineData(AppLanguage.Polish, "05.10.2026 09:30")]
    [InlineData(AppLanguage.English, "5 Oct 2026 09:30")]
    public void Should_Format_Warsaw_Time_When_Instant_Is_In_Utc(AppLanguage language, string expected)
    {
        _languageService.ChangeLanguage(language);

        var text = _formatter.FormatDateTime(new DateTimeOffset(2026, 10, 5, 7, 30, 0, TimeSpan.Zero));

        text.ShouldBe(expected);
    }

    [Theory]
    [InlineData(AppLanguage.Polish, "05.10.2026")]
    [InlineData(AppLanguage.English, "5 Oct 2026")]
    public void Should_Format_Calendar_Day_When_Language_Is_Selected(AppLanguage language, string expected)
    {
        _languageService.ChangeLanguage(language);

        _formatter.FormatDate(new DateOnly(2026, 10, 5)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(AppLanguage.Polish, 1234.5, "1 234,50 zł")]
    [InlineData(AppLanguage.Polish, -12, "-12,00 zł")]
    [InlineData(AppLanguage.English, 1234.5, "PLN 1,234.50")]
    [InlineData(AppLanguage.English, -12, "-PLN 12.00")]
    public void Should_Format_Amount_In_Pln_When_Language_Is_Selected(AppLanguage language, double amount, string expected)
    {
        _languageService.ChangeLanguage(language);

        _formatter.FormatMoney((decimal)amount).Replace(NoBreakSpace, ' ').ShouldBe(expected);
    }

    public void Dispose() => _cultureScope.Dispose();
}
