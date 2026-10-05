using FixFlow.Mobile.Core.Storage;
using SQLite;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed class DeviceRepository(UserDatabase userDatabase)
{
    private SQLiteAsyncConnection Connection => userDatabase.Current.Connection;

    public async Task UpsertAsync(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);
        await Connection.InsertOrReplaceAsync(DeviceRow.From(device));
    }

    public async Task<Device?> GetAsync(Guid id)
    {
        var rows = await Connection.QueryAsync<DeviceRow>("SELECT * FROM devices WHERE id = ?", id);
        return rows.FirstOrDefault()?.ToDevice();
    }
}
