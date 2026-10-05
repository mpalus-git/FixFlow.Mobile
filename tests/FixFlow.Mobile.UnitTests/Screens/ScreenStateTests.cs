using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Screens;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Screens;

[Collection(GlobalCultureTests.Name)]
public sealed class ScreenStateTests : IDisposable
{
    private readonly GlobalCultureScope _cultureScope = new();
    private readonly LanguageService _languageService = new(Substitute.For<ISettingsStore>());
    private readonly ScreenState _state;

    public ScreenStateTests()
    {
        _languageService.ApplySavedLanguage();
        var formatter = new DisplayFormatter(_languageService, BusinessClock.CreateForWarsaw(new FakeTimeProvider()));
        _state = new ScreenState(formatter);
    }

    [Fact]
    public void Should_Start_In_Loading_State_When_Created()
    {
        _state.Status.ShouldBe(ScreenStatus.Loading);
        _state.IsLoading.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true, ScreenStatus.Content)]
    [InlineData(false, ScreenStatus.Empty)]
    public void Should_Show_Content_Or_Empty_State_When_Loading_Finishes(bool hasItems, ScreenStatus expected)
    {
        _state.ShowContent(hasItems);

        _state.Status.ShouldBe(expected);
        _state.IsLoading.ShouldBeFalse();
    }

    [Fact]
    public void Should_Clear_Error_Message_When_Loading_Starts_Again()
    {
        _state.ShowError("Server unavailable");
        _state.IsError.ShouldBeTrue();
        _state.ErrorMessage.ShouldBe("Server unavailable");

        _state.StartLoading();

        _state.IsLoading.ShouldBeTrue();
        _state.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Should_Notify_Derived_Flags_When_Status_Changes()
    {
        var changed = new List<string?>();
        _state.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        _state.ShowContent(hasItems: false);

        changed.ShouldContain(nameof(ScreenState.IsLoading));
        changed.ShouldContain(nameof(ScreenState.IsEmpty));
    }

    [Fact]
    public void Should_Show_Last_Sync_In_Warsaw_Time_When_Offline()
    {
        _state.SetOffline(new DateTimeOffset(2026, 10, 5, 7, 30, 0, TimeSpan.Zero));

        _state.IsOffline.ShouldBeTrue();
        _state.OfflineMessage.ShouldBe("Brak połączenia. Ostatnia synchronizacja: 05.10.2026 09:30");
    }

    [Fact]
    public void Should_Explain_Missing_Data_When_Offline_And_Never_Synced()
    {
        _state.SetOffline(lastSyncedAt: null);

        _state.OfflineMessage.ShouldBe("Brak połączenia. Dane nie były jeszcze synchronizowane.");
    }

    [Fact]
    public void Should_Hide_Offline_Message_When_Back_Online()
    {
        _state.SetOffline(lastSyncedAt: null);

        _state.SetOnline();

        _state.IsOffline.ShouldBeFalse();
        _state.OfflineMessage.ShouldBeNull();
    }

    public void Dispose() => _cultureScope.Dispose();
}
