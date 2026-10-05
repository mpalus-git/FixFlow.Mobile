using System.Globalization;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.Settings;

public sealed class LanguageService(ISettingsStore settingsStore) : ILanguageService
{
    public const string LanguageKey = "language";

    private const string PolishCode = "pl";
    private const string EnglishCode = "en";

    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Polish;

    public CultureInfo CurrentCulture => CultureFor(CurrentLanguage);

    public event EventHandler? LanguageChanged;

    public void ApplySavedLanguage()
    {
        CurrentLanguage = settingsStore.GetString(LanguageKey) == EnglishCode ? AppLanguage.English : AppLanguage.Polish;
        ApplyCulture(CurrentCulture);
    }

    public void ChangeLanguage(AppLanguage language)
    {
        if (language == CurrentLanguage)
        {
            return;
        }

        CurrentLanguage = language;
        settingsStore.SetString(LanguageKey, language == AppLanguage.English ? EnglishCode : PolishCode);
        ApplyCulture(CurrentCulture);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private static CultureInfo CultureFor(AppLanguage language) =>
        CultureInfo.GetCultureInfo(language == AppLanguage.English ? "en-US" : "pl-PL");

    private static void ApplyCulture(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        AppResources.Culture = culture;
    }
}
