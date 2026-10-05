using System.Globalization;

namespace FixFlow.Mobile.Core.Features.Settings;

public interface ILanguageService
{
    AppLanguage CurrentLanguage { get; }

    CultureInfo CurrentCulture { get; }

    event EventHandler? LanguageChanged;

    void ApplySavedLanguage();

    void ChangeLanguage(AppLanguage language);
}
