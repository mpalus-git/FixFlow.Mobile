using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Accessibility;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync.Outbox;
using FixFlow.Mobile.Core.Features.WorkOrders.WorkTime;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.Core.Screens;
using FixFlow.Mobile.UnitTests.Features.WorkOrders;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.ServiceEntries;

public sealed class CorrectionEntryViewModelTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid FilterId = Guid.NewGuid();

    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly TestUserDatabase _userDatabase = new();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IScreenReaderAnnouncer _announcer = Substitute.For<IScreenReaderAnnouncer>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly WorkOrder _workOrder = TestWorkOrders.Create("ZL-1", Now.AddHours(4));
    private readonly OutboxRepository _outbox;
    private readonly OutboxWriter _writer;
    private readonly CorrectionEntryViewModel _viewModel;

    public CorrectionEntryViewModelTests()
    {
        var database = _userDatabase.Database;
        var formatter = new DisplayFormatter(new LanguageService(Substitute.For<ISettingsStore>()), BusinessClock.CreateForWarsaw(_timeProvider));
        _outbox = new OutboxRepository(database);
        _writer = new OutboxWriter(_outbox, _timeProvider, new WeakReferenceMessenger());
        _viewModel = new CorrectionEntryViewModel(
            new ScreenState(formatter, Substitute.For<IScreenReaderAnnouncer>()),
            new WorkOrderRepository(database),
            new ServiceEntryRepository(database),
            _outbox,
            _writer,
            _navigation,
            _dialogs,
            _announcer);
    }

    public async ValueTask InitializeAsync()
    {
        await _userDatabase.Database.OpenAsync(Guid.NewGuid());
        await new WorkOrderRepository(_userDatabase.Database).ReplaceAllAsync([_workOrder]);
        await _writer.StartWorkAsync(_workOrder.Id);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        await _writer.AddServiceEntryAsync(_workOrder.Id, new PendingServiceEntry(
            Guid.CreateVersion7(), "Wymiana filtra", false, Now, Now.AddMinutes(1), null, null, [new PendingServiceEntryPart(FilterId, "Filtr", "FP-100", 3, 12m)]));
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Should_List_Used_Parts_With_Net_Quantity_When_Form_Opens()
    {
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);

        _viewModel.Number.ShouldBe("ZL-1");
        var part = _viewModel.Parts.ShouldHaveSingleItem();
        part.Usage.ShouldBe(new NetPartUsage(FilterId, "Filtr", "FP-100", 3, 12m));
        part.DetailsText.ShouldBe(AppText.Format(AppResources.CorrectionNetUsed, "FP-100", 3));
        part.Quantity.ShouldBe(0);
    }

    [Fact]
    public async Task Should_Leave_Without_Asking_When_Nothing_Was_Entered()
    {
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);

        (await _viewModel.CanLeaveAsync()).ShouldBeTrue();

        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task Should_Ask_Before_Leaving_When_Returned_Quantity_Was_Set()
    {
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        _viewModel.Parts[0].IncreaseCommand.Execute(null);
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);

        (await _viewModel.CanLeaveAsync()).ShouldBeFalse();

        await _dialogs.Received(1).ConfirmAsync(
            AppResources.DiscardDraftTitle, AppResources.DiscardDraftMessage, AppResources.DiscardChangeButton, AppResources.KeepEditingButton);
    }

    [Fact]
    public async Task Should_Explain_When_No_Part_Was_Used_On_Work_Order()
    {
        var other = TestWorkOrders.Create("ZL-2", Now, WorkOrderStatus.InProgress);
        await new WorkOrderRepository(_userDatabase.Database).ReplaceAllAsync([_workOrder, other]);

        await _viewModel.OnNavigatedToAsync(other.Id);

        _viewModel.Screen.Status.ShouldBe(ScreenStatus.Error);
        _viewModel.Screen.ErrorMessage.ShouldBe(AppResources.CorrectionNothingToReturn);
    }

    [Theory]
    [InlineData("", 1, "CorrectionReasonRequired")]
    [InlineData("Pomyłka w ilości", 0, "CorrectionNoPartsSelected")]
    public async Task Should_Require_Reason_And_Returned_Part_When_Saving(string reason, int returned, string expectedKey)
    {
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        _viewModel.Reason = reason;
        for (var step = 0; step < returned; step++)
        {
            _viewModel.Parts[0].IncreaseCommand.Execute(null);
        }

        await _viewModel.SaveCommand.ExecuteAsync(null);

        _viewModel.ValidationMessage.ShouldBe(AppResources.ResourceManager.GetString(expectedKey, AppResources.Culture));
        await _navigation.DidNotReceive().GoBackAsync();
    }

    [Fact]
    public async Task Should_Announce_Validation_Message_To_Screen_Reader_When_Reason_Is_Missing()
    {
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);

        await _viewModel.SaveCommand.ExecuteAsync(null);

        _announcer.Received(1).Announce(AppResources.CorrectionReasonRequired);
    }

    [Fact]
    public async Task Should_Queue_Correction_At_Last_Price_Without_Work_Time_When_Saved()
    {
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        var timerBefore = await new WorkTimerRepository(_userDatabase.Database).GetAsync(_workOrder.Id);
        _viewModel.Reason = "  Policzono o dwa filtry za dużo  ";
        _viewModel.Parts[0].IncreaseCommand.Execute(null);
        _viewModel.Parts[0].IncreaseCommand.Execute(null);

        await _viewModel.SaveCommand.ExecuteAsync(null);

        var correction = OutboxPayload.ToServiceEntry((await _outbox.GetAllAsync())[^1]);
        correction.IsCorrection.ShouldBeTrue();
        correction.Note.ShouldBe("Policzono o dwa filtry za dużo");
        correction.WorkStartedAt.ShouldBeNull();
        correction.WorkFinishedAt.ShouldBeNull();
        correction.Latitude.ShouldBeNull();
        correction.Parts.ShouldBe([new PendingServiceEntryPart(FilterId, "Filtr", "FP-100", 2, 12m)]);
        (await new WorkTimerRepository(_userDatabase.Database).GetAsync(_workOrder.Id)).ShouldBe(timerBefore);
        await _navigation.Received(1).GoBackAsync();
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();
}
