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

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();

    private static OutboxOperation CreateOperation(OutboxOperationKind kind, DateTimeOffset createdAt) =>
        new(Guid.CreateVersion7(), WorkOrderId, kind, "{}", createdAt, 0, null, null);
}
