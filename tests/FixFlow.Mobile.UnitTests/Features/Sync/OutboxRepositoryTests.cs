using System.Net;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.UnitTests.Storage;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

public sealed class OutboxRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkOrderId = Guid.NewGuid();

    private readonly TestUserDatabase _userDatabase = new();
    private readonly OutboxRepository _repository;

    public OutboxRepositoryTests()
    {
        _repository = new OutboxRepository(_userDatabase.Database);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Return_Operations_In_Creation_Order_When_Queue_Is_Read()
    {
        var complete = CreateOperation(OutboxOperationKind.CompleteWorkOrder, Now.AddMinutes(2));
        var start = CreateOperation(OutboxOperationKind.StartWork, Now);
        var entry = CreateOperation(OutboxOperationKind.AddServiceEntry, Now.AddMinutes(1)) with { Payload = """{"note":"Wymiana filtra"}""" };
        await _repository.EnqueueAsync(complete);
        await _repository.EnqueueAsync(start);
        await _repository.EnqueueAsync(entry);

        (await _repository.GetForWorkOrderAsync(WorkOrderId)).ShouldBe([start, entry, complete]);
        (await _repository.CountPendingAsync()).ShouldBe(3);
    }

    [Fact]
    public async Task Should_Count_Attempts_And_Keep_Operation_When_Attempt_Fails()
    {
        var operation = CreateOperation(OutboxOperationKind.StartWork, Now);
        await _repository.EnqueueAsync(operation);

        await _repository.RecordFailedAttemptAsync(operation.Id, Now.AddSeconds(5), "Network is unreachable");
        await _repository.RecordFailedAttemptAsync(operation.Id, Now.AddSeconds(20), "Timeout");

        (await _repository.GetAllAsync()).ShouldBe([operation with { AttemptCount = 2, NextAttemptAt = Now.AddSeconds(20), LastError = "Timeout" }]);
    }

    [Fact]
    public async Task Should_Move_Operation_To_Conflicts_In_One_Step_When_Api_Rejects_It()
    {
        var operation = CreateOperation(OutboxOperationKind.AddServiceEntry, Now);
        await _repository.EnqueueAsync(operation);
        var conflict = new SyncConflict(
            operation.Id, WorkOrderId, operation.Kind, operation.Payload, operation.CreatedAt, HttpStatusCode.NotFound, "WorkOrder.NotFound", "Work order was not found.", Now.AddMinutes(1));

        await _repository.MoveToConflictsAsync(conflict);

        (await _repository.GetAllAsync()).ShouldBeEmpty();
        (await _repository.GetConflictsAsync()).ShouldBe([conflict]);
        (await _repository.CountConflictsAsync()).ShouldBe(1);
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();

    [Fact]
    public async Task Should_Not_Queue_Operation_When_Local_Change_Fails()
    {
        await Should.ThrowAsync<InvalidOperationException>(() =>
            _repository.EnqueueAsync(CreateOperation(OutboxOperationKind.StartWork, Now), _ => throw new InvalidOperationException()));

        (await _repository.GetAllAsync()).ShouldBeEmpty();
    }

    private static OutboxOperation CreateOperation(OutboxOperationKind kind, DateTimeOffset createdAt) =>
        new(Guid.CreateVersion7(), WorkOrderId, kind, "{}", createdAt, 0, null, null);
}
