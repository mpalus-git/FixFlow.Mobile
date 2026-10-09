using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.UnitTests.Storage;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class WorkOrderEventRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkOrderId = Guid.NewGuid();

    private readonly TestUserDatabase _userDatabase = new();
    private readonly WorkOrderEventRepository _repository;

    public WorkOrderEventRepositoryTests()
    {
        _repository = new WorkOrderEventRepository(_userDatabase.Database);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Keep_Server_Order_And_Replace_Only_Given_Work_Order_When_History_Is_Saved()
    {
        var other = CreateEvent(Guid.NewGuid(), WorkOrderEventType.Created, Now);
        await _repository.ReplaceForWorkOrderAsync(other.WorkOrderId, [other]);
        await _repository.ReplaceForWorkOrderAsync(WorkOrderId, [CreateEvent(WorkOrderId, WorkOrderEventType.Created, Now)]);

        var created = CreateEvent(WorkOrderId, WorkOrderEventType.Created, Now) with { ActorName = null };
        var assigned = CreateEvent(WorkOrderId, WorkOrderEventType.Assigned, Now);
        await _repository.ReplaceForWorkOrderAsync(WorkOrderId, [created, assigned]);

        (await _repository.GetForWorkOrderAsync(WorkOrderId)).ShouldBe([created, assigned]);
        (await _repository.GetForWorkOrderAsync(other.WorkOrderId)).ShouldBe([other]);
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();

    private static WorkOrderEvent CreateEvent(Guid workOrderId, WorkOrderEventType type, DateTimeOffset occurredAt) =>
        new(Guid.NewGuid(), workOrderId, type, occurredAt, "Anna Nowak", "Jan Kowalski", Now.AddDays(3));
}
