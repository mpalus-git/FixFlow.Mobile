using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.UnitTests.Storage;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class ClientAndDeviceRepositoryTests : IAsyncLifetime
{
    private readonly TestUserDatabase _userDatabase = new();
    private readonly ClientRepository _clients;
    private readonly DeviceRepository _devices;

    public ClientAndDeviceRepositoryTests()
    {
        _clients = new ClientRepository(_userDatabase.Database);
        _devices = new DeviceRepository(_userDatabase.Database);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Overwrite_Client_When_Server_Returns_Newer_Data()
    {
        var client = new Client(Guid.NewGuid(), "Piekarnia Kowalscy", "Długa", "12A", "31-147", "Kraków", "Anna Kowalska", "+48 600 100 200", null, null);
        await _clients.UpsertAsync(client);

        var updated = client with { Phone = "+48 600 999 888", Email = "biuro@piekarnia.test" };
        await _clients.UpsertAsync(updated);

        (await _clients.GetAsync(client.Id)).ShouldBe(updated);
    }

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
