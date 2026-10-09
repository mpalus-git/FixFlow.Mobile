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
            var signatures = connection
                .Query<WorkOrderRow>("SELECT * FROM work_orders WHERE client_signature_photo_id IS NOT NULL")
                .ToDictionary(row => row.Id, row => row.ClientSignaturePhotoId);
            foreach (var row in rows.Where(row => row.ClientSignaturePhotoId is null))
            {
                row.ClientSignaturePhotoId = signatures.GetValueOrDefault(row.Id);
            }

            connection.Execute("DELETE FROM work_orders");
            connection.InsertAll(rows, runInTransaction: false);
        });
    }

    public async Task UpsertAsync(WorkOrder workOrder)
    {
        ArgumentNullException.ThrowIfNull(workOrder);
        await Connection.InsertOrReplaceAsync(WorkOrderRow.From(workOrder));
    }

    public async Task DeleteAsync(Guid id) => await Connection.ExecuteAsync("DELETE FROM work_orders WHERE id = ?", id);

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
