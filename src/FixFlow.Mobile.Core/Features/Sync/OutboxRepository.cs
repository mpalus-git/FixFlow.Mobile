using FixFlow.Mobile.Core.Storage;
using SQLite;

namespace FixFlow.Mobile.Core.Features.Sync;

public sealed class OutboxRepository(UserDatabase userDatabase)
{
    private SQLiteAsyncConnection Connection => userDatabase.Current.Connection;

    public async Task EnqueueAsync(OutboxOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await Connection.InsertAsync(OutboxOperationRow.From(operation));
    }

    public async Task<IReadOnlyList<OutboxOperation>> GetAllAsync()
    {
        var rows = await Connection.QueryAsync<OutboxOperationRow>("SELECT * FROM outbox_operations ORDER BY created_at, id");
        return [.. rows.Select(row => row.ToOperation())];
    }

    public async Task<IReadOnlyList<OutboxOperation>> GetForWorkOrderAsync(Guid workOrderId)
    {
        var rows = await Connection.QueryAsync<OutboxOperationRow>(
            "SELECT * FROM outbox_operations WHERE work_order_id = ? ORDER BY created_at, id", workOrderId);
        return [.. rows.Select(row => row.ToOperation())];
    }

    public async Task RecordFailedAttemptAsync(Guid id, DateTimeOffset nextAttemptAt, string? lastError) =>
        await Connection.ExecuteAsync(
            "UPDATE outbox_operations SET attempt_count = attempt_count + 1, next_attempt_at = ?, last_error = ? WHERE id = ?",
            nextAttemptAt,
            lastError,
            id);

    public async Task RemoveAsync(Guid id) => await Connection.ExecuteAsync("DELETE FROM outbox_operations WHERE id = ?", id);

    public async Task MoveToConflictsAsync(SyncConflict conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        var row = SyncConflictRow.From(conflict);
        await Connection.RunInTransactionAsync(connection =>
        {
            connection.Execute("DELETE FROM outbox_operations WHERE id = ?", conflict.OperationId);
            connection.Insert(row);
        });
    }

    public async Task<IReadOnlyList<SyncConflict>> GetConflictsAsync()
    {
        var rows = await Connection.QueryAsync<SyncConflictRow>("SELECT * FROM sync_conflicts ORDER BY rejected_at, operation_id");
        return [.. rows.Select(row => row.ToConflict())];
    }

    public async Task<int> CountPendingAsync() => await Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM outbox_operations");

    public async Task<int> CountConflictsAsync() => await Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sync_conflicts");
}
