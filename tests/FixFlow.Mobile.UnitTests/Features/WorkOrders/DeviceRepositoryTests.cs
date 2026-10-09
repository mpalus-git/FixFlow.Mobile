using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.UnitTests.Storage;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class DeviceRepositoryTests : IAsyncLifetime
{
    private readonly TestUserDatabase _userDatabase = new();
    private readonly DeviceRepository _devices;

    public DeviceRepositoryTests()
    {
        _devices = new DeviceRepository(_userDatabase.Database);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Keep_Installation_Day_And_Archive_Time_When_Device_Is_Stored()
    {
        var device = new Device(Guid.NewGuid(), Guid.NewGuid(), "SN-1", "Altherma 3", "Daikin", new DateOnly(2021, 3, 14), DateTimeOffset.UnixEpoch);

        await _devices.UpsertAsync(device);

        (await _devices.GetAsync(device.Id)).ShouldBe(device);
        (await _devices.GetAsync(Guid.NewGuid())).ShouldBeNull();
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();
}
