using FixFlow.Mobile.Core.Accessibility;
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
    private readonly IScreenReaderAnnouncer _announcer = Substitute.For<IScreenReaderAnnouncer>();
    private readonly ScreenState _state;

    public ScreenStateTests()
    {
        _languageService.ApplySavedLanguage();
        var formatter = new DisplayFormatter(_languageService, BusinessClock.CreateForWarsaw(new FakeTimeProvider()));
        _state = new ScreenState(formatter, _announcer);
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

    [Fact]
    public void Should_Hide_Banner_When_There_Is_Nothing_To_Report()
    {
        _state.SetOnline();
        _state.SetQueue(pendingChanges: 0, rejectedChanges: 0);

        _state.Banner.ShouldBeNull();
        _state.HasBanner.ShouldBeFalse();
    }

    [Fact]
    public void Should_Show_Neutral_Banner_Without_Action_When_Only_Offline()
    {
        _state.SetOffline(lastSyncedAt: null);

        _state.Banner.ShouldBe(new StatusBannerView("Brak połączenia. Dane nie były jeszcze synchronizowane.", StatusTone.Neutral, HasQueueAction: false));
    }

    [Fact]
    public void Should_Show_Warning_Banner_When_Notice_Reports_Problem()
    {
        _state.ShowNotice(ScreenNotice.Problem("Nie udało się odświeżyć listy."));

        _state.Banner.ShouldBe(new StatusBannerView("Nie udało się odświeżyć listy.", StatusTone.Warning, HasQueueAction: false));
    }

    [Fact]
    public void Should_Show_Neutral_Banner_With_Queue_Action_When_Changes_Wait()
    {
        _state.SetQueue(pendingChanges: 2, rejectedChanges: 0);

        _state.Banner.ShouldBe(new StatusBannerView("Do wysłania: 2", StatusTone.Neutral, HasQueueAction: true));
    }

    [Fact]
    public void Should_Combine_All_Lines_In_One_Destructive_Banner_When_Changes_Were_Rejected()
    {
        _state.SetOffline(lastSyncedAt: null);
        _state.ShowNotice(ScreenNotice.Info("Serwer się uruchamia."));

        _state.SetQueue(pendingChanges: 2, rejectedChanges: 1);

        var banner = _state.Banner.ShouldNotBeNull();
        banner.Text.Split('\n').ShouldBe(
        [
            "Brak połączenia. Dane nie były jeszcze synchronizowane.",
            "Serwer się uruchamia.",
            "Do wysłania: 2  ·  Do wyjaśnienia: 1",
        ]);
        banner.Tone.ShouldBe(StatusTone.Destructive);
        banner.HasQueueAction.ShouldBeTrue();
    }

    [Fact]
    public void Should_Notify_Banner_When_Queue_Or_Notice_Changes()
    {
        var changed = new List<string?>();
        _state.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        _state.SetQueue(pendingChanges: 1, rejectedChanges: 0);
        _state.SetQueue(pendingChanges: 1, rejectedChanges: 0);
        _state.ShowNotice(ScreenNotice.Info("Skopiowano."));
        _state.ClearNotice();

        changed.Count(name => name == nameof(ScreenState.Banner)).ShouldBe(3);
        changed.ShouldContain(nameof(ScreenState.HasBanner));
    }

    [Fact]
    public void Should_Announce_Notice_Once_When_Same_Notice_Is_Shown_Again()
    {
        _state.ShowNotice(ScreenNotice.Problem("Nie udało się odświeżyć listy."));
        _state.ShowNotice(ScreenNotice.Problem("Nie udało się odświeżyć listy."));

        _announcer.Received(1).Announce("Nie udało się odświeżyć listy.");
    }

    public void Dispose() => _cultureScope.Dispose();
}
