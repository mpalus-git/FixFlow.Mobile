using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.UnitTests.Storage;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class DeviceHistoryRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid DeviceId = Guid.NewGuid();

    private readonly TestUserDatabase _userDatabase = new();
    private readonly DeviceHistoryRepository _repository;

    public DeviceHistoryRepositoryTests()
    {
        _repository = new DeviceHistoryRepository(_userDatabase.Database);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Return_Newest_First_And_Replace_Only_Given_Device_When_History_Is_Saved()
    {
        var otherDevice = CreateItem("ZL-2026-0001", Now) with { DeviceId = Guid.NewGuid() };
        await _repository.ReplaceForDeviceAsync(otherDevice.DeviceId, [otherDevice]);
        await _repository.ReplaceForDeviceAsync(DeviceId, [CreateItem("ZL-2026-0002", Now)]);

        var older = CreateItem("ZL-2026-0003", Now.AddDays(-30)) with { Status = WorkOrderStatus.Invoiced, CompletedAt = Now.AddDays(-29) };
        var newer = CreateItem("ZL-2026-0004", Now.AddDays(-1)) with { TechnicianName = null };
        await _repository.ReplaceForDeviceAsync(DeviceId, [older, newer]);

        (await _repository.GetForDeviceAsync(DeviceId)).ShouldBe([newer, older]);
        (await _repository.GetForDeviceAsync(otherDevice.DeviceId)).ShouldBe([otherDevice]);
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();

    private static DeviceHistoryItem CreateItem(string number, DateTimeOffset createdAt) => new(
        Guid.NewGuid(),
        DeviceId,
        number,
        WorkOrderStatus.Completed,
        WorkOrderPriority.Normal,
        "Przegląd okresowy",
        "Jan Kowalski",
        createdAt,
        createdAt.AddHours(1),
        createdAt.AddHours(3),
        2);
}
