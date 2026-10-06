using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Connectivity;
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
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly WorkOrder _workOrder = TestWorkOrders.Create("ZL-2026-0042", Now.AddDays(1));
    private readonly OutboxRepository _outbox;
    private readonly SyncViewModel _viewModel;

    public SyncViewModelTests()
    {
        var formatter = new DisplayFormatter(new LanguageService(Substitute.For<ISettingsStore>()), BusinessClock.CreateForWarsaw(_timeProvider));
        _outbox = new OutboxRepository(_userDatabase.Database);
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
            new SyncPresenter(new LocalChangeDescriber(formatter), formatter),
            formatter,
            _connectivity,
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
    public async Task Should_Show_Offline_State_When_Synchronization_Is_Requested_Without_Network()
    {
        _connectivity.IsOnline.Returns(false);

        await _viewModel.SynchronizeCommand.ExecuteAsync(null);

        _viewModel.Screen.IsOffline.ShouldBeTrue();
        _viewModel.IsRefreshing.ShouldBeFalse();
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();
}
