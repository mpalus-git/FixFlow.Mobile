using FixFlow.Mobile.Core.Storage;
using SQLite;

namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public sealed class ServiceEntryRepository(UserDatabase userDatabase)
{
    private const string PartsOfWorkOrder =
        "service_entry_id IN (SELECT id FROM service_entries WHERE work_order_id = ?)";

    private SQLiteAsyncConnection Connection => userDatabase.Current.Connection;

    public async Task ReplaceForWorkOrderAsync(Guid workOrderId, IReadOnlyCollection<ServiceEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var entryRows = entries.Select(ServiceEntryRow.From).ToList();
        var partRows = entries
            .SelectMany(entry => entry.Parts.Select((part, position) => ServiceEntryPartRow.From(entry.Id, position, part)))
            .ToList();
        await Connection.RunInTransactionAsync(connection =>
        {
            connection.Execute($"DELETE FROM service_entry_parts WHERE {PartsOfWorkOrder}", workOrderId);
            connection.Execute("DELETE FROM service_entries WHERE work_order_id = ?", workOrderId);
            connection.InsertAll(entryRows, runInTransaction: false);
            connection.InsertAll(partRows, runInTransaction: false);
        });
    }

    public async Task UpsertAsync(ServiceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var entryRow = ServiceEntryRow.From(entry);
        var partRows = entry.Parts.Select((part, position) => ServiceEntryPartRow.From(entry.Id, position, part)).ToList();
        await Connection.RunInTransactionAsync(connection =>
        {
            connection.Execute("DELETE FROM service_entry_parts WHERE service_entry_id = ?", entry.Id);
            connection.InsertOrReplace(entryRow);
            connection.InsertAll(partRows, runInTransaction: false);
        });
    }

    public async Task<IReadOnlyList<ServiceEntry>> GetForWorkOrderAsync(Guid workOrderId)
    {
        var entryRows = await Connection.QueryAsync<ServiceEntryRow>(
            "SELECT * FROM service_entries WHERE work_order_id = ? ORDER BY created_at, id", workOrderId);
        var partRows = await Connection.QueryAsync<ServiceEntryPartRow>(
            $"SELECT * FROM service_entry_parts WHERE {PartsOfWorkOrder} ORDER BY position", workOrderId);
        var partsByEntry = partRows.ToLookup(row => row.ServiceEntryId, row => row.ToPart());

        return [.. entryRows.Select(row => row.ToServiceEntry([.. partsByEntry[row.Id]]))];
    }
}
