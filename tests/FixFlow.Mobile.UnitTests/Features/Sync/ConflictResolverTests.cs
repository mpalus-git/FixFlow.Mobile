using System.Net;
using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync.Conflicts;
using FixFlow.Mobile.Core.Features.Sync.Outbox;
using FixFlow.Mobile.Core.Features.WorkOrders.WorkTime;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

public sealed class ConflictResolverTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkOrderId = Guid.NewGuid();
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 1];

    private readonly TestUserDatabase _userDatabase = new();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly List<LocalChangeQueuedMessage> _messages = [];
    private readonly OutboxRepository _outbox;
    private readonly OutboxWriter _writer;
    private readonly PhotoStore _photos;
    private readonly WorkTimerRepository _timers;
    private readonly ConflictResolver _resolver;

    public ConflictResolverTests()
    {
        _outbox = new OutboxRepository(_userDatabase.Database);
        _writer = new OutboxWriter(_outbox, _timeProvider, _messenger);
        _photos = new PhotoStore(_userDatabase.Options, _userDatabase.Database);
        _timers = new WorkTimerRepository(_userDatabase.Database);
        _resolver = new ConflictResolver(_outbox, _photos, _messenger);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Put_Operation_Back_At_Front_Of_Queue_When_Conflict_Is_Retried()
    {
        var entry = await QueueEntryAsync([]);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        var complete = await _writer.CompleteWorkOrderAsync(WorkOrderId);
        await RejectAsync(entry);
        _messenger.Register<ConflictResolverTests, LocalChangeQueuedMessage>(this, (recipient, message) => recipient._messages.Add(message));

        await _resolver.RetryAsync(entry.Id);

        (await _outbox.GetForWorkOrderAsync(WorkOrderId)).ShouldBe([entry, complete]);
        (await _outbox.GetConflictsAsync()).ShouldBeEmpty();
        _messages.ShouldBe([new LocalChangeQueuedMessage(WorkOrderId)]);
    }

    [Fact]
    public async Task Should_Release_Following_Operations_When_Only_Rejected_Change_Is_Discarded()
    {
        var entry = await QueueEntryAsync([]);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        var complete = await _writer.CompleteWorkOrderAsync(WorkOrderId);
        await RejectAsync(entry);

        await _resolver.DiscardAsync(entry.Id);

        (await _outbox.GetConflictsAsync()).ShouldBeEmpty();
        (await _outbox.GetAllAsync()).ShouldBe([complete]);
    }

    [Fact]
    public async Task Should_Remove_Work_Timer_When_Rejected_Start_Is_Discarded()
    {
        var start = await _writer.StartWorkAsync(WorkOrderId);
        await RejectAsync(start);

        await _resolver.DiscardAsync(start.Id);

        (await _timers.GetAsync(WorkOrderId)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Remove_Photo_From_Waiting_Entry_When_Rejected_Upload_Is_Discarded()
    {
        PendingPhoto missing = new(Guid.CreateVersion7(), "https://api.test/api/v1/photos/1");
        PendingPhoto kept = new(Guid.CreateVersion7(), "https://api.test/api/v1/photos/2");
        await _photos.SaveAsync(kept.Id, Jpeg);
        var entry = await QueueEntryAsync([missing, kept]);
        var upload = (await _outbox.GetAllAsync())[0];
        await RejectAsync(upload);

        await _resolver.DiscardAsync(upload.Id);

        var waiting = await _outbox.GetAllAsync();
        waiting.Select(operation => operation.Kind).ShouldBe([OutboxOperationKind.UploadPhoto, OutboxOperationKind.AddServiceEntry]);
        OutboxPayload.ToServiceEntry(waiting[1]).Photos.ShouldBe([kept]);
        waiting[1].Id.ShouldBe(entry.Id);
        _photos.ExistingPathFor(kept.Id).ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Delete_Photo_Files_When_Rejected_Entry_Is_Discarded()
    {
        PendingPhoto photo = new(Guid.CreateVersion7(), "https://api.test/api/v1/photos/1");
        await _photos.SaveAsync(photo.Id, Jpeg);
        var entry = await QueueEntryAsync([photo]);
        await _outbox.RemoveAsync((await _outbox.GetAllAsync())[0].Id);
        await RejectAsync(entry);

        await _resolver.DiscardAsync(entry.Id);

        _photos.ExistingPathFor(photo.Id).ShouldBeNull();
        (await _outbox.GetAllAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Remove_Every_Change_Of_Work_Order_When_All_Its_Changes_Are_Discarded()
    {
        var otherWorkOrderId = Guid.NewGuid();
        var start = await _writer.StartWorkAsync(WorkOrderId);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        PendingPhoto photo = new(Guid.CreateVersion7(), "https://api.test/api/v1/photos/1");
        await _photos.SaveAsync(photo.Id, Jpeg);
        await QueueEntryAsync([photo]);
        var other = await _writer.CompleteWorkOrderAsync(otherWorkOrderId);
        await RejectAsync(start);

        await _resolver.DiscardWorkOrderChangesAsync(WorkOrderId);

        (await _outbox.GetConflictsAsync()).ShouldBeEmpty();
        (await _outbox.GetAllAsync()).ShouldBe([other]);
        (await _timers.GetAsync(WorkOrderId)).ShouldBeNull();
        _photos.ExistingPathFor(photo.Id).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Complete_Without_Signature_When_Rejected_Signature_Upload_Is_Discarded()
    {
        var signatureId = Guid.CreateVersion7();
        await _photos.SaveAsync(signatureId, Jpeg);
        var completion = await _writer.CompleteWorkOrderAsync(WorkOrderId, signatureId);
        var upload = (await _outbox.GetAllAsync())[0];
        await RejectAsync(upload);

        await _resolver.DiscardAsync(upload.Id);

        var queued = (await _outbox.GetAllAsync()).ShouldHaveSingleItem();
        queued.Id.ShouldBe(completion.Id);
        OutboxPayload.ClientSignaturePhotoId(queued).ShouldBeNull();
        OutboxPayload.CompletedAt(queued).ShouldBe(OutboxPayload.CompletedAt(completion));
        _photos.ExistingPathFor(signatureId).ShouldBeNull();
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();

    private async Task<OutboxOperation> QueueEntryAsync(IReadOnlyList<PendingPhoto> photos) =>
        await _writer.AddServiceEntryAsync(
            WorkOrderId,
            new PendingServiceEntry(Guid.CreateVersion7(), "Wymiana filtra", false, Now.AddHours(-1), Now, null, null, []) { Photos = photos });

    private Task RejectAsync(OutboxOperation operation) =>
        _outbox.MoveToConflictsAsync(new SyncConflict(
            operation.Id,
            operation.WorkOrderId,
            operation.Kind,
            operation.Payload,
            operation.CreatedAt,
            HttpStatusCode.Conflict,
            "Part.InsufficientStock",
            null,
            Now));
}
