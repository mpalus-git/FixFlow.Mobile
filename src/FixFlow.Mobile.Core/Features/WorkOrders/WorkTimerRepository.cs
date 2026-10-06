using FixFlow.Mobile.Core.Storage;
using SQLite;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed class WorkTimerRepository(UserDatabase userDatabase)
{
    private SQLiteAsyncConnection Connection => userDatabase.Current.Connection;

    public async Task<WorkTimer?> GetAsync(Guid workOrderId)
    {
        var rows = await Connection.QueryAsync<WorkTimerRow>("SELECT * FROM work_timers WHERE work_order_id = ?", workOrderId);
        return rows.FirstOrDefault()?.ToWorkTimer();
    }

    public async Task SetLocationAsync(Guid workOrderId, WorkLocationStatus status, double? latitude, double? longitude) =>
        await Connection.ExecuteAsync(
            "UPDATE work_timers SET location_status = ?, latitude = ?, longitude = ? WHERE work_order_id = ?",
            status.ToString(),
            latitude,
            longitude,
            workOrderId);
}
