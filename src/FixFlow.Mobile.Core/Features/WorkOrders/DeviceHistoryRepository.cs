using FixFlow.Mobile.Core.Storage;
using SQLite;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed class DeviceHistoryRepository(UserDatabase userDatabase)
{
    private SQLiteAsyncConnection Connection => userDatabase.Current.Connection;

    public async Task ReplaceForDeviceAsync(Guid deviceId, IReadOnlyCollection<DeviceHistoryItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var rows = items.Select(DeviceHistoryRow.From).ToList();
        await Connection.RunInTransactionAsync(connection =>
        {
            connection.Execute("DELETE FROM device_work_orders WHERE device_id = ?", deviceId);
            connection.InsertAll(rows, runInTransaction: false);
        });
    }

    public async Task<IReadOnlyList<DeviceHistoryItem>> GetForDeviceAsync(Guid deviceId)
    {
        var rows = await Connection.QueryAsync<DeviceHistoryRow>(
            "SELECT * FROM device_work_orders WHERE device_id = ? ORDER BY created_at DESC, work_order_id DESC",
            deviceId);
        return [.. rows.Select(row => row.ToItem())];
    }
}
