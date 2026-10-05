using System.Globalization;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Resources;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.Settings;

[Collection(GlobalCultureTests.Name)]
public sealed class LanguageServiceTests : IDisposable
{
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly LanguageService _service;

    public LanguageServiceTests()
    {
        _service = new LanguageService(_settingsStore);
    }

    [Fact]
    public void Should_Use_Polish_When_No_Language_Is_Saved()
    {
        _service.ApplySavedLanguage();

        _service.CurrentLanguage.ShouldBe(AppLanguage.Polish);
        CultureInfo.CurrentUICulture.Name.ShouldBe("pl-PL");
        AppResources.WorkOrderListTitle.ShouldBe("Moje zlecenia");
    }

    [Fact]
    public void Should_Use_English_When_English_Was_Saved()
    {
        _settingsStore.GetString(LanguageService.LanguageKey).Returns("en");

        _service.ApplySavedLanguage();

        _service.CurrentLanguage.ShouldBe(AppLanguage.English);
        CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
        AppResources.WorkOrderListTitle.ShouldBe("My work orders");
    }

    [Fact]
    public void Should_Save_Language_And_Raise_Event_When_Language_Changes()
    {
        _service.ApplySavedLanguage();
        var raised = 0;
        _service.LanguageChanged += (_, _) => raised++;

        _service.ChangeLanguage(AppLanguage.English);

        _settingsStore.Received(1).SetString(LanguageService.LanguageKey, "en");
        _service.CurrentCulture.Name.ShouldBe("en-US");
        raised.ShouldBe(1);
    }

    [Fact]
    public void Should_Not_Save_Or_Raise_Event_When_Same_Language_Is_Selected()
    {
        _service.ApplySavedLanguage();
        var raised = 0;
        _service.LanguageChanged += (_, _) => raised++;

        _service.ChangeLanguage(AppLanguage.Polish);

        _settingsStore.DidNotReceiveWithAnyArgs().SetString(default!, default!);
        raised.ShouldBe(0);
    }

    public void Dispose()
    {
        CultureInfo.DefaultThreadCurrentCulture = null;
        CultureInfo.DefaultThreadCurrentUICulture = null;
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUiCulture;
        AppResources.Culture = null;
    }
}
