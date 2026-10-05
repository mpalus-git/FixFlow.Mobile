using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.UnitTests.Api;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Refit;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

public sealed class SyncCoordinatorTests : IAsyncLifetime
{
    private const string ListCall = "GET /api/v1/work-orders";
    private static readonly Guid WorkOrderId = Guid.NewGuid();

    private readonly TestUserDatabase _userDatabase = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero));
    private readonly IConnectivityService _connectivity = Substitute.For<IConnectivityService>();
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly OutboxApiStub _api = new();
    private readonly StubHttpMessageHandler _handler;
    private readonly OutboxRepository _outbox;
    private readonly SyncCoordinator _coordinator;

    public SyncCoordinatorTests()
    {
        _connectivity.IsOnline.Returns(true);
        _handler = new StubHttpMessageHandler(_api.RouteAsync);
        var httpClient = new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };
        var client = RestService.ForGenerated<IFixFlowApi>(httpClient);
        var database = _userDatabase.Database;
        _outbox = new OutboxRepository(database);
        var warmup = new ServerWarmup();
        _coordinator = new SyncCoordinator(
            database,
            _connectivity,
            warmup,
            () => new ServerReadinessProbe(httpClient, _timeProvider),
            () => new OutboxDispatcher(client, _outbox, new OutboxRetryPolicy(() => 0.5), _timeProvider),
            () => new WorkOrderSyncService(client, new ServerReadinessProbe(httpClient, _timeProvider), warmup, new WorkOrderRepository(database), new SyncStateRepository(database, _timeProvider)),
            _messenger);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Send_Queue_Before_Pulling_Work_Orders_When_Synchronizing()
    {
        await new OutboxWriter(_outbox, _timeProvider, new WeakReferenceMessenger()).StartWorkAsync(WorkOrderId);

        var result = await _coordinator.SynchronizeAsync();

        result.Dispatch!.Sent.ShouldBe(1);
        result.Pull.ShouldBe(PullResult.Succeeded);
        _api.Calls.ShouldBe(["GET /api/v1/system/ready", $"POST /api/v1/work-orders/{WorkOrderId}/start", ListCall]);
    }

    public async ValueTask DisposeAsync()
    {
        _handler.Dispose();
        await _userDatabase.DisposeAsync();
    }
}
