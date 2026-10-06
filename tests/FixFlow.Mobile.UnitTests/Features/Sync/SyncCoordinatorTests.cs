using System.Net;
using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Features.Parts;
using FixFlow.Mobile.Core.Features.ServiceEntries;
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
            () => new OutboxDispatcher(client, _outbox, new ServiceEntryRepository(_userDatabase.Database), new PhotoUploadClient(httpClient), new PhotoStore(_userDatabase.Options, _userDatabase.Database), new OutboxRetryPolicy(() => 0.5), _timeProvider),
            () => new WorkOrderSyncService(client, new ServerReadinessProbe(httpClient, _timeProvider), warmup, new WorkOrderRepository(database), new SyncStateRepository(database, _timeProvider)),
            () => new PartSyncService(client, new PartRepository(database), new SyncStateRepository(database, _timeProvider)),
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
        _api.Calls.ShouldBe(["GET /api/v1/system/ready", $"POST /api/v1/work-orders/{WorkOrderId}/start", ListCall, "GET /api/v1/parts"]);
    }

    [Fact]
    public async Task Should_Run_Once_More_Instead_Of_Each_Time_When_Requested_During_Sync()
    {
        var release = new TaskCompletionSource();
        var firstList = true;
        _api.AsyncOverrides[ListCall] = async () =>
        {
            if (firstList)
            {
                firstList = false;
                await release.Task;
            }

            return OutboxApiStub.Json(HttpStatusCode.OK, """{"items":[],"page":1,"pageSize":100,"totalCount":0}""");
        };

        var first = _coordinator.SynchronizeAsync();
        while (!_api.Calls.Contains(ListCall))
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        var second = _coordinator.SynchronizeAsync();
        var third = _coordinator.SynchronizeAsync();
        release.SetResult();
        await Task.WhenAll(first, second, third);

        _api.Calls.Count(call => call == ListCall).ShouldBe(2);
    }

    [Fact]
    public async Task Should_Report_Offline_Without_Requests_When_Device_Is_Offline()
    {
        _connectivity.IsOnline.Returns(false);
        SyncCompletedMessage? completed = null;
        _messenger.Register<SyncCompletedMessage>(this, (_, message) => completed = message);

        var result = await _coordinator.SynchronizeAsync();

        result.ShouldBe(SyncResult.Offline);
        completed.ShouldNotBeNull().Result.ShouldBe(SyncResult.Offline);
        _api.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Synchronize_When_Connection_Returns()
    {
        _coordinator.Start();
        var completed = WaitForCompletionAsync();

        _messenger.Send(new ConnectivityChangedMessage(IsOnline: true));

        await completed;
        _api.Calls.ShouldContain(ListCall);
    }

    [Fact]
    public async Task Should_Synchronize_When_Local_Change_Is_Queued()
    {
        _coordinator.Start();
        var completed = WaitForCompletionAsync();

        await new OutboxWriter(_outbox, _timeProvider, _messenger).StartWorkAsync(WorkOrderId);

        await completed;
        _api.Calls.ShouldContain($"POST /api/v1/work-orders/{WorkOrderId}/start");
    }

    [Fact]
    public async Task Should_Skip_Pull_When_Session_Is_Rejected_While_Sending()
    {
        await new OutboxWriter(_outbox, _timeProvider, new WeakReferenceMessenger()).StartWorkAsync(WorkOrderId);
        _api.Overrides[$"POST /api/v1/work-orders/{WorkOrderId}/start"] = () => new HttpResponseMessage(HttpStatusCode.Unauthorized);

        var result = await _coordinator.SynchronizeAsync();

        result.Pull.ShouldBeNull();
        _api.Calls.ShouldNotContain(ListCall);
    }

    public async ValueTask DisposeAsync()
    {
        _handler.Dispose();
        await _userDatabase.DisposeAsync();
    }

    private Task WaitForCompletionAsync()
    {
        var completed = new TaskCompletionSource();
        _messenger.Register<SyncCompletedMessage>(this, (_, _) => completed.TrySetResult());
        return completed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }
}
