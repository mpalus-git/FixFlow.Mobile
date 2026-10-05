using FixFlow.Mobile.Core.Storage;
using SQLite;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed class WorkOrderRepository(UserDatabase userDatabase)
{
    private SQLiteAsyncConnection Connection => userDatabase.Current.Connection;

    public async Task ReplaceAllAsync(IReadOnlyCollection<WorkOrder> workOrders)
    {
        ArgumentNullException.ThrowIfNull(workOrders);

        var rows = workOrders.Select(WorkOrderRow.From).ToList();
        await Connection.RunInTransactionAsync(connection =>
        {
            connection.Execute("DELETE FROM work_orders");
            connection.InsertAll(rows, runInTransaction: false);
        });
    }

    public async Task<IReadOnlyList<WorkOrder>> GetAllAsync()
    {
        var rows = await Connection.QueryAsync<WorkOrderRow>("SELECT * FROM work_orders ORDER BY due_date, number");
        return [.. rows.Select(row => row.ToWorkOrder())];
    }

    public async Task<WorkOrder?> GetAsync(Guid id)
    {
        var rows = await Connection.QueryAsync<WorkOrderRow>("SELECT * FROM work_orders WHERE id = ?", id);
        return rows.FirstOrDefault()?.ToWorkOrder();
    }
}
