using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.UnitTests.Storage;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class WorkOrderRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 30, 15, TimeSpan.Zero);

    private readonly TestUserDatabase _userDatabase = new();
    private readonly WorkOrderRepository _repository;

    public WorkOrderRepositoryTests()
    {
        _repository = new WorkOrderRepository(_userDatabase.Database);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Read_Work_Order_Exactly_As_Saved_When_Stored_Locally()
    {
        var workOrder = CreateWorkOrder("ZL-2026-0042", Now.AddHours(4)) with
        {
            Status = WorkOrderStatus.InProgress,
            Priority = WorkOrderPriority.Critical,
            StartedAt = new DateTimeOffset(2026, 10, 5, 7, 15, 30, 123, TimeSpan.Zero),
        };

        await _repository.ReplaceAllAsync([workOrder]);

        (await _repository.GetAsync(workOrder.Id)).ShouldBe(workOrder);
    }

    [Fact]
    public async Task Should_Replace_Local_Copy_When_Server_Returns_New_List()
    {
        var removed = CreateWorkOrder("ZL-2026-0001", Now);
        var kept = CreateWorkOrder("ZL-2026-0002", Now.AddDays(1));
        await _repository.ReplaceAllAsync([removed, kept]);

        var updated = kept with { Status = WorkOrderStatus.Completed, CompletedAt = Now };
        await _repository.ReplaceAllAsync([updated]);

        (await _repository.GetAllAsync()).ShouldBe([updated]);
        (await _repository.GetAsync(removed.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Order_Work_Orders_By_Due_Date_When_Reading_All()
    {
        var later = CreateWorkOrder("ZL-2026-0003", Now.AddDays(2));
        var sooner = CreateWorkOrder("ZL-2026-0004", Now.AddHours(1));

        await _repository.ReplaceAllAsync([later, sooner]);

        (await _repository.GetAllAsync()).Select(workOrder => workOrder.Number).ShouldBe(["ZL-2026-0004", "ZL-2026-0003"]);
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();

    private static WorkOrder CreateWorkOrder(string number, DateTimeOffset dueDate) => new(
        Guid.NewGuid(),
        number,
        Guid.NewGuid(),
        "SN-123",
        "Daikin Altherma 3",
        Guid.NewGuid(),
        "Piekarnia Kowalscy",
        "Przegląd pompy ciepła",
        WorkOrderPriority.Normal,
        WorkOrderStatus.Assigned,
        dueDate,
        false,
        Now.AddDays(-3),
        null,
        null,
        null);
}
