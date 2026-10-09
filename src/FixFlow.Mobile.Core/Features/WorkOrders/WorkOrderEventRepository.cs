using FixFlow.Mobile.Core.Storage;
using SQLite;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed class WorkOrderEventRepository(UserDatabase userDatabase)
{
    private SQLiteAsyncConnection Connection => userDatabase.Current.Connection;

    public async Task ReplaceForWorkOrderAsync(Guid workOrderId, IReadOnlyList<WorkOrderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var rows = events.Select(WorkOrderEventRow.From).ToList();
        await Connection.RunInTransactionAsync(connection =>
        {
            connection.Execute("DELETE FROM work_order_events WHERE work_order_id = ?", workOrderId);
            connection.InsertAll(rows, runInTransaction: false);
        });
    }

    public async Task<IReadOnlyList<WorkOrderEvent>> GetForWorkOrderAsync(Guid workOrderId)
    {
        var rows = await Connection.QueryAsync<WorkOrderEventRow>(
            "SELECT * FROM work_order_events WHERE work_order_id = ? ORDER BY position",
            workOrderId);
        return [.. rows.Select(row => row.ToEvent())];
    }
}
