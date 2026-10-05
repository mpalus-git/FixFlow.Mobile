using System.Globalization;
using FixFlow.Mobile.Core.Features.Settings;

namespace FixFlow.Mobile.Core.Formatting;

public sealed class DisplayFormatter(ILanguageService languageService, BusinessClock clock)
{
    private const string PolishDatePattern = "dd.MM.yyyy";
    private const string PolishDateTimePattern = "dd.MM.yyyy HH:mm";
    private const string EnglishDatePattern = "d MMM yyyy";
    private const string EnglishDateTimePattern = "d MMM yyyy HH:mm";
    private const string EnglishCurrencySymbol = "PLN";
    private const int SymbolSpaceNumberPattern = 2;
    private const int MinusSymbolSpaceNumberPattern = 9;

    private bool IsEnglish => languageService.CurrentLanguage == AppLanguage.English;

    public string FormatDateTime(DateTimeOffset instant) =>
        clock.ToBusinessTime(instant).ToString(
            IsEnglish ? EnglishDateTimePattern : PolishDateTimePattern,
            languageService.CurrentCulture);

    public string FormatTime(DateTimeOffset instant) =>
        clock.ToBusinessTime(instant).ToString("HH:mm", languageService.CurrentCulture);

    public string FormatDate(DateOnly date) =>
        date.ToString(IsEnglish ? EnglishDatePattern : PolishDatePattern, languageService.CurrentCulture);

    public string FormatMoney(decimal amount) => amount.ToString("C", MoneyFormat());

    private NumberFormatInfo MoneyFormat()
    {
        var format = (NumberFormatInfo)languageService.CurrentCulture.NumberFormat.Clone();
        if (IsEnglish)
        {
            format.CurrencySymbol = EnglishCurrencySymbol;
            format.CurrencyPositivePattern = SymbolSpaceNumberPattern;
            format.CurrencyNegativePattern = MinusSymbolSpaceNumberPattern;
        }

        return format;
    }
}
