using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Features.Parts;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
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

public sealed class ServiceEntryViewModelTests : IAsyncLifetime
{
    private static readonly DateTimeOffset StartedAt = new(2026, 10, 5, 8, 0, 30, TimeSpan.Zero);
    private static readonly Part Filter = new(Guid.NewGuid(), "Filtr powietrza", "FP-100", 5, 12.34m, null);

    private readonly FakeTimeProvider _timeProvider = new(StartedAt);
    private readonly TestUserDatabase _userDatabase = new();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly WorkOrder _workOrder = TestWorkOrders.Create("ZL-1", StartedAt.AddHours(4));
    private readonly OutboxRepository _outbox;
    private readonly WorkTimerRepository _timers;
    private readonly DisplayFormatter _formatter;
    private readonly ServiceEntryViewModel _viewModel;

    public ServiceEntryViewModelTests()
    {
        var database = _userDatabase.Database;
        var clock = BusinessClock.CreateForWarsaw(_timeProvider);
        _formatter = new DisplayFormatter(new LanguageService(Substitute.For<ISettingsStore>()), clock);
        _outbox = new OutboxRepository(database);
        _timers = new WorkTimerRepository(database);
        _viewModel = new ServiceEntryViewModel(
            new ScreenState(_formatter),
            new ServiceEntryPartsSelection(new PartRepository(database), _formatter),
            new WorkOrderRepository(database),
            _outbox,
            _timers,
            new OutboxWriter(_outbox, _timeProvider, new WeakReferenceMessenger()),
            clock,
            _formatter,
            _navigation);
    }

    public async ValueTask InitializeAsync()
    {
        await _userDatabase.Database.OpenAsync(Guid.NewGuid());
        await new WorkOrderRepository(_userDatabase.Database).ReplaceAllAsync([_workOrder]);
        await new PartRepository(_userDatabase.Database).ReplaceAllAsync([Filter]);
        await new OutboxWriter(_outbox, _timeProvider, new WeakReferenceMessenger()).StartWorkAsync(_workOrder.Id);
        _timeProvider.Advance(TimeSpan.FromMinutes(90));
    }

    [Fact]
    public async Task Should_Prefill_Work_Time_From_Timer_Start_To_Now_When_Form_Opens()
    {
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);

        _viewModel.Number.ShouldBe("ZL-1");
        _viewModel.StartDate.ShouldBe(new DateTime(2026, 10, 5));
        _viewModel.StartTime.ShouldBe(new TimeSpan(10, 0, 0));
        _viewModel.FinishTime.ShouldBe(new TimeSpan(11, 30, 0));
        _viewModel.LocationText.ShouldBe(AppResources.EntryWithoutLocation);
        _viewModel.Parts.Results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Show_Error_When_Work_Order_Is_Not_In_Progress()
    {
        var assigned = TestWorkOrders.Create("ZL-2", StartedAt);
        await new WorkOrderRepository(_userDatabase.Database).ReplaceAllAsync([_workOrder, assigned]);

        await _viewModel.OnNavigatedToAsync(assigned.Id);

        _viewModel.Screen.Status.ShouldBe(ScreenStatus.Error);
        _viewModel.Screen.ErrorMessage.ShouldBe(AppResources.EntryRequiresWorkInProgress);
    }

    [Fact]
    public async Task Should_Require_Note_And_Queue_Nothing_When_Note_Is_Empty()
    {
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        _viewModel.Note = "   ";

        await _viewModel.SaveCommand.ExecuteAsync(null);

        _viewModel.ValidationMessage.ShouldBe(AppResources.EntryNoteRequired);
        (await _outbox.GetAllAsync()).ShouldHaveSingleItem().Kind.ShouldBe(OutboxOperationKind.StartWork);
        await _navigation.DidNotReceive().GoBackAsync();
    }

    [Fact]
    public async Task Should_Reject_Start_When_It_Is_Earlier_Than_Start_Of_Work_Order()
    {
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        _viewModel.Note = "Przegląd";
        _viewModel.StartTime = new TimeSpan(9, 30, 0);

        await _viewModel.SaveCommand.ExecuteAsync(null);

        _viewModel.ValidationMessage.ShouldBe(WorkOrderTexts.Format(AppResources.EntryStartBeforeWorkOrder, _formatter.FormatDateTime(StartedAt)));
    }

    [Theory]
    [InlineData(9, 59, "EntryFinishBeforeStart")]
    [InlineData(11, 45, "EntryFinishInFuture")]
    public async Task Should_Reject_Finish_When_It_Is_Before_Start_Or_In_Future(int hour, int minute, string expectedKey)
    {
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        _viewModel.Note = "Przegląd";
        _viewModel.FinishTime = new TimeSpan(hour, minute, 0);

        await _viewModel.SaveCommand.ExecuteAsync(null);

        _viewModel.ValidationMessage.ShouldBe(AppResources.ResourceManager.GetString(expectedKey, AppResources.Culture));
    }

    [Fact]
    public async Task Should_Queue_Entry_With_Position_And_Parts_And_Go_Back_When_Form_Is_Valid()
    {
        await _timers.SetLocationAsync(_workOrder.Id, WorkLocationStatus.Captured, 50.06, 19.94);
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        _viewModel.Note = "  Wymiana filtra  ";
        _viewModel.StartTime = new TimeSpan(10, 0, 0);
        _viewModel.Parts.SearchText = "filtr";
        await _viewModel.Parts.SearchAsync();
        await _viewModel.Parts.AddCommand.ExecuteAsync(_viewModel.Parts.Results[0]);
        _viewModel.Parts.Selected[0].IncreaseCommand.Execute(null);

        await _viewModel.SaveCommand.ExecuteAsync(null);

        _viewModel.HasValidationMessage.ShouldBeFalse();
        var entry = OutboxPayload.ToServiceEntry((await _outbox.GetAllAsync()).Single(operation => operation.Kind == OutboxOperationKind.AddServiceEntry));
        entry.Note.ShouldBe("Wymiana filtra");
        entry.WorkStartedAt.ShouldBe(StartedAt);
        entry.WorkFinishedAt.ShouldBe(_timeProvider.GetUtcNow());
        (entry.Latitude, entry.Longitude).ShouldBe((50.06, 19.94));
        entry.Parts.ShouldHaveSingleItem().Quantity.ShouldBe(2);
        await _navigation.Received(1).GoBackAsync();
    }

    [Fact]
    public async Task Should_Send_Entry_Without_Position_When_Earlier_Entry_Already_Used_It()
    {
        await _timers.SetLocationAsync(_workOrder.Id, WorkLocationStatus.NotRequired, null, null);
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        _viewModel.Note = "Druga część pracy";

        await _viewModel.SaveCommand.ExecuteAsync(null);

        _viewModel.HasLocationText.ShouldBeFalse();
        var entry = OutboxPayload.ToServiceEntry((await _outbox.GetAllAsync()).Single(operation => operation.Kind == OutboxOperationKind.AddServiceEntry));
        entry.Latitude.ShouldBeNull();
        entry.Longitude.ShouldBeNull();
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();
}
