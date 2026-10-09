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
    private const string ReadyCall = "GET /api/v1/system/ready";
    private const string PartsCall = "GET /api/v1/parts";
    private static readonly Guid WorkOrderId = Guid.NewGuid();
    private static readonly string StartCall = $"POST /api/v1/work-orders/{WorkOrderId}/start";

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
            () => new WorkOrderSyncService(client, new WorkOrderRepository(database), new SyncStateRepository(database, _timeProvider)),
            () => new PartSyncService(client, new PartRepository(database), new SyncStateRepository(database, _timeProvider)),
            _messenger,
            _timeProvider);
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

    [Fact]
    public async Task Should_Check_Server_Readiness_Only_Before_First_Sync()
    {
        await _coordinator.SynchronizeAsync();
        await _coordinator.SynchronizeAsync();

        _api.Calls.Count(call => call == ReadyCall).ShouldBe(1);
        _api.Calls.Count(call => call == ListCall).ShouldBe(2);
    }

    [Fact]
    public async Task Should_Report_Server_Unavailable_Without_Requests_When_Server_Does_Not_Start()
    {
        await new OutboxWriter(_outbox, _timeProvider, new WeakReferenceMessenger()).StartWorkAsync(WorkOrderId);
        _api.Overrides[ReadyCall] = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var startingNotices = 0;
        _messenger.Register<ServerStartingMessage>(this, (_, _) => startingNotices++);

        var sync = _coordinator.SynchronizeAsync();
        while (!sync.IsCompleted)
        {
            _timeProvider.Advance(TimeSpan.FromSeconds(3));
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        (await sync).ShouldBe(SyncResult.ServerUnavailable);
        startingNotices.ShouldBe(1);
        _api.Calls.ShouldAllBe(call => call == ReadyCall);
        (await _outbox.GetAllAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Should_Send_Operation_Again_When_Retry_Time_Comes()
    {
        await new OutboxWriter(_outbox, _timeProvider, new WeakReferenceMessenger()).StartWorkAsync(WorkOrderId);
        _api.Overrides[StartCall] = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        (await _coordinator.SynchronizeAsync()).Dispatch!.Remaining.ShouldBe(1);
        _api.Overrides.Remove(StartCall);

        _timeProvider.Advance(TimeSpan.FromSeconds(4));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        _api.Calls.Count(call => call == StartCall).ShouldBe(1);

        var completed = WaitForCompletionAsync();
        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        await completed;

        _api.Calls.Count(call => call == StartCall).ShouldBe(2);
        (await _outbox.GetAllAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Schedule_Retry_When_Operation_Already_Waits_For_Retry()
    {
        var operation = await new OutboxWriter(_outbox, _timeProvider, new WeakReferenceMessenger()).StartWorkAsync(WorkOrderId);
        await _outbox.RecordFailedAttemptAsync(operation.Id, _timeProvider.GetUtcNow().AddMinutes(1), "503");
        await _coordinator.SynchronizeAsync();
        _api.Calls.ShouldNotContain(StartCall);

        var completed = WaitForCompletionAsync();
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        await completed;

        _api.Calls.ShouldContain(StartCall);
    }

    [Fact]
    public async Task Should_Retry_Only_After_Resume_When_Retries_Are_Suspended()
    {
        await new OutboxWriter(_outbox, _timeProvider, new WeakReferenceMessenger()).StartWorkAsync(WorkOrderId);
        _api.Overrides[StartCall] = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        _coordinator.SuspendRetries();
        await _coordinator.SynchronizeAsync();
        _api.Overrides.Remove(StartCall);

        _timeProvider.Advance(TimeSpan.FromMinutes(15));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        _api.Calls.Count(call => call == StartCall).ShouldBe(1);

        await _coordinator.ResumeAsync();

        _api.Calls.Count(call => call == StartCall).ShouldBe(2);
    }

    [Fact]
    public async Task Should_Not_Schedule_Retry_When_Session_Is_Rejected()
    {
        await new OutboxWriter(_outbox, _timeProvider, new WeakReferenceMessenger()).StartWorkAsync(WorkOrderId);
        var operation = (await _outbox.GetAllAsync()).ShouldHaveSingleItem();
        await _outbox.RecordFailedAttemptAsync(operation.Id, _timeProvider.GetUtcNow(), "503");
        _api.Overrides[StartCall] = () => new HttpResponseMessage(HttpStatusCode.Unauthorized);
        await _coordinator.SynchronizeAsync();

        _timeProvider.Advance(TimeSpan.FromMinutes(15));
        await Task.Delay(50, TestContext.Current.CancellationToken);

        _api.Calls.Count(call => call == StartCall).ShouldBe(1);
    }

    [Fact]
    public async Task Should_Skip_Part_Catalog_When_It_Was_Pulled_Recently()
    {
        await _coordinator.SynchronizeAsync();
        _timeProvider.Advance(SyncCoordinator.PartCatalogMaxAge - TimeSpan.FromSeconds(1));

        await _coordinator.SynchronizeAsync();

        _api.Calls.Count(call => call == ListCall).ShouldBe(2);
        _api.Calls.Count(call => call == PartsCall).ShouldBe(1);
    }

    [Fact]
    public async Task Should_Pull_Part_Catalog_When_It_Is_Older_Than_Limit()
    {
        await _coordinator.SynchronizeAsync();
        _timeProvider.Advance(SyncCoordinator.PartCatalogMaxAge);

        await _coordinator.SynchronizeAsync();

        _api.Calls.Count(call => call == PartsCall).ShouldBe(2);
    }

    [Fact]
    public async Task Should_Pull_Part_Catalog_When_Sent_Entry_Changed_Part_Stock()
    {
        await _coordinator.SynchronizeAsync();
        var now = _timeProvider.GetUtcNow();
        await new OutboxWriter(_outbox, _timeProvider, new WeakReferenceMessenger()).AddServiceEntryAsync(
            WorkOrderId,
            new PendingServiceEntry(Guid.CreateVersion7(), "Wymiana filtra", false, now.AddHours(-1), now, null, null, [new PendingServiceEntryPart(Guid.NewGuid(), "Filtr", "FP-100", 1, 12.34m)]));

        await _coordinator.SynchronizeAsync();

        _api.Calls.Count(call => call == PartsCall).ShouldBe(2);
    }

    [Fact]
    public async Task Should_Pull_Part_Catalog_When_User_Requests_Refresh()
    {
        await _coordinator.SynchronizeAsync();

        await _coordinator.SynchronizeAsync(refreshPartCatalog: true);

        _api.Calls.Count(call => call == PartsCall).ShouldBe(2);
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
