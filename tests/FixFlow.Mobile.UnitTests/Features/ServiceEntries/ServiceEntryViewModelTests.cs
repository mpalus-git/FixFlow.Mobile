using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Features.Parts;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
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
    private readonly WorkOrder _workOrder = TestWorkOrders.Create("ZL-1", StartedAt.AddHours(4));
    private readonly OutboxRepository _outbox;
    private readonly WorkTimerRepository _timers;
    private readonly ServiceEntryViewModel _viewModel;

    public ServiceEntryViewModelTests()
    {
        var database = _userDatabase.Database;
        var clock = BusinessClock.CreateForWarsaw(_timeProvider);
        var formatter = new DisplayFormatter(new LanguageService(Substitute.For<ISettingsStore>()), clock);
        _outbox = new OutboxRepository(database);
        _timers = new WorkTimerRepository(database);
        _viewModel = new ServiceEntryViewModel(
            new ScreenState(formatter),
            new ServiceEntryPartsSelection(new PartRepository(database), formatter),
            new WorkOrderRepository(database),
            _outbox,
            _timers,
            clock);
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
        _viewModel.Parts.Results.ShouldHaveSingleItem().Part.Id.ShouldBe(Filter.Id);
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

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();
}
