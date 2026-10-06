using System.Net;
using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Clipboard;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Screens;
using FixFlow.Mobile.UnitTests.Features.WorkOrders;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

public sealed class SyncViewModelTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly TestUserDatabase _userDatabase = new();
    private readonly IConnectivityService _connectivity = Substitute.For<IConnectivityService>();
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly WorkOrder _workOrder = TestWorkOrders.Create("ZL-2026-0042", Now.AddDays(1));
    private readonly OutboxRepository _outbox;
    private readonly OutboxWriter _writer;
    private readonly SyncViewModel _viewModel;

    public SyncViewModelTests()
    {
        var formatter = new DisplayFormatter(new LanguageService(Substitute.For<ISettingsStore>()), BusinessClock.CreateForWarsaw(_timeProvider));
        _outbox = new OutboxRepository(_userDatabase.Database);
        _writer = new OutboxWriter(_outbox, _timeProvider, _messenger);
        var coordinator = new SyncCoordinator(
            _userDatabase.Database,
            _connectivity,
            new ServerWarmup(),
            () => throw new InvalidOperationException(),
            () => throw new InvalidOperationException(),
            () => throw new InvalidOperationException(),
            () => throw new InvalidOperationException(),
            _messenger);
        _viewModel = new SyncViewModel(
            new ScreenState(formatter),
            _outbox,
            new WorkOrderRepository(_userDatabase.Database),
            new SyncStateRepository(_userDatabase.Database, _timeProvider),
            coordinator,
            new ConflictResolver(_outbox, new PhotoStore(_userDatabase.Options, _userDatabase.Database), _messenger),
            new SyncPresenter(new LocalChangeDescriber(formatter), formatter),
            formatter,
            _connectivity,
            _clipboard,
            _dialogs,
            new ImmediateUiDispatcher(),
            _messenger);
    }

    public async ValueTask InitializeAsync()
    {
        await _userDatabase.Database.OpenAsync(Guid.NewGuid());
        await new WorkOrderRepository(_userDatabase.Database).ReplaceAllAsync([_workOrder]);
    }

    [Fact]
    public async Task Should_Show_Empty_State_When_Everything_Was_Sent()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.Screen.Status.ShouldBe(ScreenStatus.Empty);
        _viewModel.HasConflicts.ShouldBeFalse();
        _viewModel.HasPendingOperations.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_List_Conflict_And_Held_Operation_When_Change_Was_Rejected()
    {
        await RejectStartAsync();

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.Screen.Status.ShouldBe(ScreenStatus.Content);
        _viewModel.Conflicts.ShouldHaveSingleItem().FollowingChangeCount.ShouldBe(1);
        _viewModel.PendingOperations.ShouldHaveSingleItem().IsHeld.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Copy_Change_Content_When_Copy_Is_Requested()
    {
        await RejectStartAsync();
        await _viewModel.LoadCommand.ExecuteAsync(null);
        var conflict = _viewModel.Conflicts[0];

        await _viewModel.CopyCommand.ExecuteAsync(conflict);

        await _clipboard.Received(1).SetTextAsync(conflict.Content);
        _viewModel.HasNotice.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Move_Conflict_Back_To_Queue_When_Retry_Is_Requested()
    {
        var start = await RejectStartAsync();
        await _viewModel.LoadCommand.ExecuteAsync(null);

        await _viewModel.RetryCommand.ExecuteAsync(_viewModel.Conflicts[0]);

        _viewModel.Conflicts.ShouldBeEmpty();
        _viewModel.PendingOperations.Select(operation => (operation.OperationId, operation.IsHeld)).First().ShouldBe((start.Id, false));
    }

    [Fact]
    public async Task Should_Keep_Conflict_When_Discard_Is_Not_Confirmed()
    {
        await RejectStartAsync();
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);

        await _viewModel.DiscardCommand.ExecuteAsync(_viewModel.Conflicts[0]);

        (await _outbox.GetConflictsAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Should_Release_Held_Operation_When_Discard_Is_Confirmed()
    {
        await RejectStartAsync();
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);

        await _viewModel.DiscardCommand.ExecuteAsync(_viewModel.Conflicts[0]);

        _viewModel.Conflicts.ShouldBeEmpty();
        _viewModel.PendingOperations.ShouldHaveSingleItem().IsHeld.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Name_Number_Of_Lost_Changes_When_All_Work_Order_Changes_Are_Discarded()
    {
        await RejectStartAsync();
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);

        await _viewModel.DiscardAllCommand.ExecuteAsync(_viewModel.Conflicts[0]);

        await _dialogs.Received(1).ConfirmAsync(Arg.Any<string>(), Arg.Is<string>(message => message.EndsWith(": 2", StringComparison.Ordinal)), Arg.Any<string>(), Arg.Any<string>());
        _viewModel.Screen.Status.ShouldBe(ScreenStatus.Empty);
    }

    [Fact]
    public async Task Should_Refresh_Last_Synchronization_When_Background_Sync_Completes()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        await new SyncStateRepository(_userDatabase.Database, _timeProvider).SetAsync(WorkOrderSyncService.SyncStateKey, "pulled");
        var shown = new TaskCompletionSource();
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SyncViewModel.LastSyncText) && _viewModel.LastSyncText!.Contains("10:05", StringComparison.Ordinal))
            {
                shown.TrySetResult();
            }
        };

        _messenger.Send(new SyncCompletedMessage(SyncResult.Offline));

        await shown.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_Show_Offline_State_When_Synchronization_Is_Requested_Without_Network()
    {
        _connectivity.IsOnline.Returns(false);

        await _viewModel.SynchronizeCommand.ExecuteAsync(null);

        _viewModel.Screen.IsOffline.ShouldBeTrue();
        _viewModel.IsRefreshing.ShouldBeFalse();
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();

    private async Task<OutboxOperation> RejectStartAsync()
    {
        var start = await _writer.StartWorkAsync(_workOrder.Id);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        await _writer.CompleteWorkOrderAsync(_workOrder.Id);
        await _outbox.MoveToConflictsAsync(new SyncConflict(
            start.Id, start.WorkOrderId, start.Kind, start.Payload, start.CreatedAt, HttpStatusCode.Conflict, "WorkOrder.TechnicianAlreadyHasWorkInProgress", null, Now));
        return start;
    }
}
