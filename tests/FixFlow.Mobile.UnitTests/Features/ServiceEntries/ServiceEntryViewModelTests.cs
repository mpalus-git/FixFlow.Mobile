using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Accessibility;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.Parts;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Photos;
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
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1];
    private static readonly Part Filter = new(Guid.NewGuid(), "Filtr powietrza", "FP-100", 5, 12.34m, null);

    private readonly FakeTimeProvider _timeProvider = new(StartedAt);
    private readonly TestUserDatabase _userDatabase = new();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IScreenReaderAnnouncer _announcer = Substitute.For<IScreenReaderAnnouncer>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly WorkOrder _workOrder = TestWorkOrders.Create("ZL-1", StartedAt.AddHours(4));
    private readonly OutboxRepository _outbox;
    private readonly WorkTimerRepository _timers;
    private readonly DisplayFormatter _formatter;
    private readonly IPhotoService _photoService = Substitute.For<IPhotoService>();
    private readonly PhotoStore _photos;
    private readonly ServiceEntryViewModel _viewModel;

    public ServiceEntryViewModelTests()
    {
        var database = _userDatabase.Database;
        var clock = BusinessClock.CreateForWarsaw(_timeProvider);
        _formatter = new DisplayFormatter(new LanguageService(Substitute.For<ISettingsStore>()), clock);
        _outbox = new OutboxRepository(database);
        _timers = new WorkTimerRepository(database);
        _photos = new PhotoStore(_userDatabase.Options, database);
        _photoService.HasCameraPermissionAsync().Returns(true);
        _photoService.IsCaptureSupported.Returns(true);
        _viewModel = new ServiceEntryViewModel(
            new ScreenState(_formatter),
            new ServiceEntryPartsSelection(new PartRepository(database), _formatter),
            new PhotoAttacher(_photoService, _photos, new ApiAddress(new Uri("https://api.test")), Substitute.For<IDialogService>()),
            new WorkOrderRepository(database),
            new ServiceEntryRepository(database),
            _outbox,
            _timers,
            new OutboxWriter(_outbox, _timeProvider, new WeakReferenceMessenger()),
            clock,
            _formatter,
            _navigation,
            _dialogs,
            _announcer);
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
    public async Task Should_Start_From_Last_Entry_Finish_When_Work_Was_Started_On_Other_Device()
    {
        await new OutboxWriter(_outbox, _timeProvider, new WeakReferenceMessenger()).AddServiceEntryAsync(
            _workOrder.Id, new PendingServiceEntry(Guid.CreateVersion7(), "Pierwsza część", false, StartedAt, StartedAt.AddMinutes(46), null, null, []));
        await _userDatabase.Database.Current.Connection.ExecuteAsync("DELETE FROM work_timers");

        await _viewModel.OnNavigatedToAsync(_workOrder.Id);

        _viewModel.StartTime.ShouldBe(new TimeSpan(10, 46, 0));
    }

    [Fact]
    public async Task Should_Start_After_Entry_Saved_On_Other_Device_When_Local_Timer_Is_Older()
    {
        var finishedElsewhere = StartedAt.AddMinutes(30);
        await new ServiceEntryRepository(_userDatabase.Database).UpsertAsync(new ServiceEntry(
            Guid.CreateVersion7(), _workOrder.Id, Guid.NewGuid(), "Jan Kowalski", "Z telefonu", false, StartedAt, finishedElsewhere, null, null, finishedElsewhere, []));

        await _viewModel.OnNavigatedToAsync(_workOrder.Id);

        _viewModel.StartTime.ShouldBe(new TimeSpan(10, 30, 0));
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
    public async Task Should_Announce_Validation_Message_To_Screen_Reader_When_Entry_Is_Invalid()
    {
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        _viewModel.Note = string.Empty;

        await _viewModel.SaveCommand.ExecuteAsync(null);

        _announcer.Received(1).Announce(AppResources.EntryNoteRequired);
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

    [Fact]
    public async Task Should_Queue_Photo_Upload_Before_Entry_When_Photo_Is_Taken()
    {
        _photoService.CaptureAsync(Arg.Any<CancellationToken>()).Returns(Jpeg);
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        _viewModel.Note = "Zdjęcie tabliczki znamionowej";

        await _viewModel.TakePhotoCommand.ExecuteAsync(null);
        await _viewModel.SaveCommand.ExecuteAsync(null);

        var draft = _viewModel.Photos.ShouldHaveSingleItem();
        File.Exists(draft.LocalPath).ShouldBeTrue();
        var queued = await _outbox.GetAllAsync();
        queued.Select(operation => operation.Kind).ShouldBe([OutboxOperationKind.StartWork, OutboxOperationKind.UploadPhoto, OutboxOperationKind.AddServiceEntry]);
        OutboxPayload.ToServiceEntry(queued[2]).Photos.ShouldHaveSingleItem().ShouldBe(draft.Photo);
    }

    [Fact]
    public async Task Should_Leave_Without_Asking_When_Form_Was_Not_Changed()
    {
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);

        (await _viewModel.CanLeaveAsync()).ShouldBeTrue();

        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task Should_Stay_On_Form_When_Technician_Keeps_Unsaved_Note()
    {
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        _viewModel.Note = "Wymiana filtra";
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);

        (await _viewModel.CanLeaveAsync()).ShouldBeFalse();

        _viewModel.Note.ShouldBe("Wymiana filtra");
    }

    [Fact]
    public async Task Should_Ask_When_Only_Work_Time_Was_Changed()
    {
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        _viewModel.FinishTime = _viewModel.FinishTime!.Value.Add(TimeSpan.FromMinutes(-5));

        await _viewModel.CanLeaveAsync();

        await _dialogs.ReceivedWithAnyArgs(1).ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task Should_Delete_Draft_Photos_When_Technician_Discards_Entry()
    {
        _photoService.CaptureAsync(Arg.Any<CancellationToken>()).Returns(Jpeg);
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        await _viewModel.TakePhotoCommand.ExecuteAsync(null);
        var draft = _viewModel.Photos[0];
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);

        (await _viewModel.CanLeaveAsync()).ShouldBeTrue();

        File.Exists(draft.LocalPath).ShouldBeFalse();
        (await _outbox.GetAllAsync()).ShouldAllBe(operation => operation.Kind == OutboxOperationKind.StartWork);
    }

    [Fact]
    public async Task Should_Leave_Without_Asking_When_Entry_Was_Saved()
    {
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        _viewModel.Note = "Wymiana filtra";
        await _viewModel.SaveCommand.ExecuteAsync(null);

        (await _viewModel.CanLeaveAsync()).ShouldBeTrue();

        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task Should_Delete_Photo_File_When_Technician_Removes_It()
    {
        _photoService.CaptureAsync(Arg.Any<CancellationToken>()).Returns(Jpeg);
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        await _viewModel.TakePhotoCommand.ExecuteAsync(null);
        var draft = _viewModel.Photos[0];

        _viewModel.RemovePhotoCommand.Execute(draft);

        _viewModel.HasPhotos.ShouldBeFalse();
        File.Exists(draft.LocalPath).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Let_Technician_Pick_Only_Remaining_Photos_When_Some_Are_Attached()
    {
        _photoService.CaptureAsync(Arg.Any<CancellationToken>()).Returns(Jpeg);
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        await _viewModel.TakePhotoCommand.ExecuteAsync(null);

        await _viewModel.PickPhotosCommand.ExecuteAsync(null);

        await _photoService.Received(1).PickAsync(ServiceEntryViewModel.MaxPhotos - 1, Arg.Any<CancellationToken>());
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();
}
