using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class WorkTimerRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkOrderId = Guid.NewGuid();

    private readonly TestUserDatabase _userDatabase = new();
    private readonly WorkTimerRepository _repository;

    public WorkTimerRepositoryTests()
    {
        _repository = new WorkTimerRepository(_userDatabase.Database);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Return_Null_When_Work_Order_Has_No_Timer()
    {
        (await _repository.GetAsync(WorkOrderId)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Store_Position_When_Location_Is_Captured()
    {
        await StartWorkAsync();

        await _repository.SetLocationAsync(WorkOrderId, WorkLocationStatus.Captured, 50.0614, 19.9366);

        (await _repository.GetAsync(WorkOrderId)).ShouldBe(new WorkTimer(WorkOrderId, Now, WorkLocationStatus.Captured, 50.0614, 19.9366));
    }

    [Fact]
    public async Task Should_Keep_Start_Time_Without_Position_When_Location_Is_Denied()
    {
        await StartWorkAsync();

        await _repository.SetLocationAsync(WorkOrderId, WorkLocationStatus.Denied, null, null);

        (await _repository.GetAsync(WorkOrderId)).ShouldBe(new WorkTimer(WorkOrderId, Now, WorkLocationStatus.Denied, null, null));
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();

    private Task<OutboxOperation> StartWorkAsync() =>
        new OutboxWriter(new OutboxRepository(_userDatabase.Database), new FakeTimeProvider(Now), new WeakReferenceMessenger()).StartWorkAsync(WorkOrderId);
}
