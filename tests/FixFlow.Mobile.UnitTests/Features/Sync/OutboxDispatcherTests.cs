using System.Net;
using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.UnitTests.Api;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;
using Refit;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

public sealed class OutboxDispatcherTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkOrderId = Guid.NewGuid();
    private static readonly Guid OtherWorkOrderId = Guid.NewGuid();

    private readonly TestUserDatabase _userDatabase = new();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly OutboxApiStub _api = new();
    private readonly StubHttpMessageHandler _handler;
    private readonly OutboxRepository _outbox;
    private readonly OutboxWriter _writer;
    private readonly OutboxDispatcher _dispatcher;
    private readonly PhotoStore _photos;

    public OutboxDispatcherTests()
    {
        _handler = new StubHttpMessageHandler(_api.RouteAsync);
        _outbox = new OutboxRepository(_userDatabase.Database);
        _writer = new OutboxWriter(_outbox, _timeProvider, new WeakReferenceMessenger());
        var httpClient = new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };
        _photos = new PhotoStore(_userDatabase.Options, _userDatabase.Database);
        _dispatcher = new OutboxDispatcher(
            RestService.ForGenerated<IFixFlowApi>(httpClient),
            _outbox,
            new ServiceEntryRepository(_userDatabase.Database),
            new PhotoUploadClient(httpClient),
            _photos,
            new OutboxRetryPolicy(() => 0.5),
            _timeProvider);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Send_Work_Order_Operations_In_Creation_Order_With_Entry_Client_Id()
    {
        await QueueAsync(_writer.StartWorkAsync(WorkOrderId));
        var entry = await AddEntryAsync(WorkOrderId);
        await QueueAsync(_writer.CompleteWorkOrderAsync(WorkOrderId));

        var summary = await DispatchAsync();

        summary.ShouldBe(new DispatchSummary(Sent: 3, Rejected: 0, Remaining: 0, SessionRejected: false) { ChangedPartStock = true });
        _api.Calls.ShouldBe([$"POST /api/v1/work-orders/{WorkOrderId}/start", $"POST /api/v1/work-orders/{WorkOrderId}/service-entries", $"POST /api/v1/work-orders/{WorkOrderId}/complete"]);
        var sent = _api.SentEntries.ShouldHaveSingleItem().RootElement;
        sent.GetProperty("id").GetGuid().ShouldBe(entry.Id);
        sent.GetProperty("parts")[0].GetProperty("quantity").GetInt32().ShouldBe(2);
        (await _outbox.GetAllAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Keep_Sent_Entry_Visible_Locally_When_Api_Accepts_It()
    {
        var entry = await AddEntryAsync(WorkOrderId);

        await DispatchAsync();

        var stored = (await new ServiceEntryRepository(_userDatabase.Database).GetForWorkOrderAsync(WorkOrderId)).ShouldHaveSingleItem();
        stored.Id.ShouldBe(entry.Id);
        stored.TechnicianName.ShouldBe("Jan Kowalski");
        (await _outbox.GetAllAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Upload_Photo_Before_Entry_And_Delete_File_When_Queue_Is_Sent()
    {
        var photo = await SavePhotoAsync();
        await AddEntryAsync(WorkOrderId, photo);

        var summary = await DispatchAsync();

        summary.Sent.ShouldBe(2);
        _api.Calls.ShouldBe([$"PUT /api/v1/photos/{photo.Id}", $"POST /api/v1/work-orders/{WorkOrderId}/service-entries"]);
        _api.SentEntries.ShouldHaveSingleItem().RootElement.GetProperty("photoUrls")[0].GetString().ShouldBe(photo.Url);
        (await _photos.ReadAsync(photo.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Hold_Entry_Behind_Conflict_When_Photo_File_Is_Missing()
    {
        await AddEntryAsync(WorkOrderId, new PendingPhoto(Guid.CreateVersion7(), "https://api.test/api/v1/photos/missing"));

        var summary = await DispatchAsync();

        summary.Rejected.ShouldBe(1);
        _api.Calls.ShouldBeEmpty();
        var conflict = (await _outbox.GetConflictsAsync()).ShouldHaveSingleItem();
        conflict.Kind.ShouldBe(OutboxOperationKind.UploadPhoto);
        conflict.ErrorCode.ShouldBe(OutboxDispatcher.MissingPhotoFileCode);
        (await _outbox.GetAllAsync()).ShouldHaveSingleItem().Kind.ShouldBe(OutboxOperationKind.AddServiceEntry);
    }

    [Fact]
    public async Task Should_Keep_Photo_File_And_Entry_When_Upload_Fails_Transiently()
    {
        var photo = await SavePhotoAsync();
        await AddEntryAsync(WorkOrderId, photo);
        _api.Overrides[$"PUT /api/v1/photos/{photo.Id}"] = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        var summary = await DispatchAsync();

        summary.Remaining.ShouldBe(2);
        _api.Calls.ShouldHaveSingleItem();
        (await _photos.ReadAsync(photo.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Send_Start_Time_From_Device_When_Start_Is_Sent_Later()
    {
        var startedOffline = _timeProvider.GetUtcNow();
        await QueueAsync(_writer.StartWorkAsync(WorkOrderId));
        _timeProvider.Advance(TimeSpan.FromHours(2));

        await DispatchAsync();

        _api.SentStarts.ShouldHaveSingleItem().RootElement.GetProperty("startedAt").GetDateTimeOffset().ShouldBe(startedOffline);
    }

    [Fact]
    public async Task Should_Send_Completion_Time_From_Device_When_Completion_Is_Sent_Later()
    {
        var completedOffline = _timeProvider.GetUtcNow();
        await QueueAsync(_writer.CompleteWorkOrderAsync(WorkOrderId));
        _timeProvider.Advance(TimeSpan.FromHours(2));

        await DispatchAsync();

        _api.SentCompletions.ShouldHaveSingleItem().RootElement.GetProperty("completedAt").GetDateTimeOffset().ShouldBe(completedOffline);
    }

    [Fact]
    public async Task Should_Upload_Signature_Before_Completion_And_Send_Its_Id()
    {
        var signature = await SavePhotoAsync();
        await QueueAsync(_writer.CompleteWorkOrderAsync(WorkOrderId, signature.Id));

        var summary = await DispatchAsync();

        summary.Sent.ShouldBe(2);
        _api.Calls.ShouldBe([$"PUT /api/v1/photos/{signature.Id}", $"POST /api/v1/work-orders/{WorkOrderId}/complete"]);
        _api.SentCompletions.ShouldHaveSingleItem().RootElement.GetProperty("clientSignaturePhotoId").GetGuid().ShouldBe(signature.Id);
        (await _photos.ReadAsync(signature.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Send_Queue_Time_As_Completion_Time_When_Queued_Completion_Has_No_Time()
    {
        var queuedAt = _timeProvider.GetUtcNow();
        await _outbox.EnqueueAsync(
            new OutboxOperation(Guid.CreateVersion7(), WorkOrderId, OutboxOperationKind.CompleteWorkOrder, OutboxPayload.Empty, queuedAt, 0, null, null));
        _timeProvider.Advance(TimeSpan.FromHours(2));

        await DispatchAsync();

        _api.SentCompletions.ShouldHaveSingleItem().RootElement.GetProperty("completedAt").GetDateTimeOffset().ShouldBe(queuedAt);
    }

    [Fact]
    public async Task Should_Keep_Operation_And_Hold_Later_Ones_When_Server_Is_Unavailable()
    {
        await QueueAsync(_writer.StartWorkAsync(WorkOrderId));
        await AddEntryAsync(WorkOrderId);
        _api.Overrides[$"POST /api/v1/work-orders/{WorkOrderId}/start"] = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var dispatchedAt = _timeProvider.GetUtcNow();

        var summary = await DispatchAsync();

        summary.Remaining.ShouldBe(2);
        _api.Calls.ShouldHaveSingleItem();
        var waiting = (await _outbox.GetAllAsync())[0];
        waiting.AttemptCount.ShouldBe(1);
        waiting.NextAttemptAt.ShouldBe(dispatchedAt.AddSeconds(5));
    }

    [Fact]
    public async Task Should_Send_Other_Work_Orders_When_One_Work_Order_Waits_For_Retry()
    {
        await QueueAsync(_writer.StartWorkAsync(WorkOrderId));
        await QueueAsync(_writer.CompleteWorkOrderAsync(OtherWorkOrderId));
        _api.Overrides[$"POST /api/v1/work-orders/{WorkOrderId}/start"] = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        var summary = await DispatchAsync();

        summary.Sent.ShouldBe(1);
        _api.Calls.ShouldContain($"POST /api/v1/work-orders/{OtherWorkOrderId}/complete");
    }

    [Fact]
    public async Task Should_Not_Report_Part_Stock_Change_When_Sent_Entry_Has_No_Parts()
    {
        await QueueAsync(_writer.AddServiceEntryAsync(
            WorkOrderId, new PendingServiceEntry(Guid.CreateVersion7(), "Przegląd", false, Now.AddHours(-1), Now, null, null, [])));

        var summary = await DispatchAsync();

        summary.Sent.ShouldBe(1);
        summary.ChangedPartStock.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Report_Earliest_Retry_Time_When_Operations_Wait_For_Retry()
    {
        await QueueAsync(_writer.StartWorkAsync(WorkOrderId));
        var waiting = await _writer.CompleteWorkOrderAsync(OtherWorkOrderId);
        await _outbox.RecordFailedAttemptAsync(waiting.Id, Now.AddSeconds(3), "503");
        _api.Overrides[$"POST /api/v1/work-orders/{WorkOrderId}/start"] = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        var summary = await DispatchAsync();

        summary.Remaining.ShouldBe(2);
        summary.NextAttemptAt.ShouldBe(Now.AddSeconds(3));
        _api.Calls.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Should_Wait_For_Retry_After_When_Server_Limits_Requests()
    {
        await QueueAsync(_writer.StartWorkAsync(WorkOrderId));
        _api.Overrides[$"POST /api/v1/work-orders/{WorkOrderId}/start"] = () =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.Add("Retry-After", "120");
            return response;
        };

        var dispatchedAt = _timeProvider.GetUtcNow();
        await DispatchAsync();
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        await DispatchAsync();

        _api.Calls.Count.ShouldBe(1);
        (await _outbox.GetAllAsync())[0].NextAttemptAt.ShouldBe(dispatchedAt.AddMinutes(2));
    }

    [Fact]
    public async Task Should_Move_Operation_To_Conflicts_And_Block_Later_Ones_When_Api_Returns_404()
    {
        await AddEntryAsync(WorkOrderId);
        await QueueAsync(_writer.CompleteWorkOrderAsync(WorkOrderId));
        _api.Overrides[$"POST /api/v1/work-orders/{WorkOrderId}/service-entries"] = () => OutboxApiStub.Json(
            HttpStatusCode.NotFound, """{"status":404,"detail":"Work order was not found.","errorCode":"WorkOrder.NotFound"}""");

        var first = await DispatchAsync();
        _api.Overrides.Clear();
        var second = await DispatchAsync();

        first.ShouldBe(new DispatchSummary(Sent: 0, Rejected: 1, Remaining: 1, SessionRejected: false));
        second.Sent.ShouldBe(0);
        var conflict = (await _outbox.GetConflictsAsync()).ShouldHaveSingleItem();
        conflict.ErrorCode.ShouldBe("WorkOrder.NotFound");
        conflict.Kind.ShouldBe(OutboxOperationKind.AddServiceEntry);
        (await _outbox.GetAllAsync()).ShouldHaveSingleItem().Kind.ShouldBe(OutboxOperationKind.CompleteWorkOrder);
        _api.Calls.ShouldNotContain($"POST /api/v1/work-orders/{WorkOrderId}/complete");
    }

    [Fact]
    public async Task Should_Move_Start_To_Conflicts_Without_Reading_Work_Order_When_Api_Returns_409()
    {
        await QueueAsync(_writer.StartWorkAsync(WorkOrderId));
        _api.Overrides[$"POST /api/v1/work-orders/{WorkOrderId}/start"] = () => OutboxApiStub.Json(
            HttpStatusCode.Conflict, """{"status":409,"errorCode":"WorkOrder.TechnicianHasWorkInProgress"}""");

        await DispatchAsync();

        (await _outbox.GetConflictsAsync()).ShouldHaveSingleItem().ErrorCode.ShouldBe("WorkOrder.TechnicianHasWorkInProgress");
        _api.Calls.ShouldBe([$"POST /api/v1/work-orders/{WorkOrderId}/start"]);
    }

    [Fact]
    public async Task Should_Stop_And_Keep_Queue_When_Session_Is_Rejected()
    {
        await QueueAsync(_writer.StartWorkAsync(WorkOrderId));
        await QueueAsync(_writer.CompleteWorkOrderAsync(OtherWorkOrderId));
        _api.Overrides[$"POST /api/v1/work-orders/{WorkOrderId}/start"] = () => new HttpResponseMessage(HttpStatusCode.Unauthorized);

        var summary = await DispatchAsync();

        summary.SessionRejected.ShouldBeTrue();
        _api.Calls.ShouldHaveSingleItem();
        (await _outbox.GetAllAsync()).Count.ShouldBe(2);
        (await _outbox.GetAllAsync())[0].AttemptCount.ShouldBe(0);
    }

    public async ValueTask DisposeAsync()
    {
        _handler.Dispose();
        await _userDatabase.DisposeAsync();
    }

    private async Task QueueAsync(Task<OutboxOperation> enqueue)
    {
        await enqueue;
        _timeProvider.Advance(TimeSpan.FromMilliseconds(1));
    }

    private async Task<PendingServiceEntry> AddEntryAsync(Guid workOrderId, params PendingPhoto[] photos)
    {
        var entry = new PendingServiceEntry(
            Guid.CreateVersion7(), "Wymiana filtra", false, Now.AddHours(-1), Now, null, null,
            [new PendingServiceEntryPart(Guid.NewGuid(), "Filtr", "FP-100", 2, 12.34m)]) { Photos = photos };
        await _writer.AddServiceEntryAsync(workOrderId, entry);
        _timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        return entry;
    }

    private async Task<PendingPhoto> SavePhotoAsync()
    {
        var photoId = Guid.CreateVersion7();
        await _photos.SaveAsync(photoId, [0xFF, 0xD8, 0xFF, 0xE0]);
        return new PendingPhoto(photoId, $"https://api.test/api/v1/photos/{photoId}");
    }

    private Task<DispatchSummary> DispatchAsync() => _dispatcher.DispatchAsync(TestContext.Current.CancellationToken);
}
