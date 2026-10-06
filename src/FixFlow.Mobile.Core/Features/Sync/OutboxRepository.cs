using FixFlow.Mobile.Core.Storage;
using SQLite;

namespace FixFlow.Mobile.Core.Features.Sync;

public sealed class OutboxRepository(UserDatabase userDatabase)
{
    private SQLiteAsyncConnection Connection => userDatabase.Current.Connection;

    public Task EnqueueAsync(OutboxOperation operation, Action<SQLiteConnection>? localChange = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return EnqueueAsync([operation], localChange);
    }

    public async Task EnqueueAsync(IReadOnlyList<OutboxOperation> operations, Action<SQLiteConnection>? localChange = null)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var rows = operations.Select(OutboxOperationRow.From).ToList();
        await Connection.RunInTransactionAsync(connection =>
        {
            localChange?.Invoke(connection);
            connection.InsertAll(rows, runInTransaction: false);
        });
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

    public async Task<SyncConflict?> RequeueConflictAsync(Guid operationId)
    {
        SyncConflict? conflict = null;
        await Connection.RunInTransactionAsync(connection =>
        {
            conflict = TakeConflict(connection, operationId);
            if (conflict is not null)
            {
                connection.Insert(OutboxOperationRow.From(conflict.ToOperation()));
            }
        });
        return conflict;
    }

    public async Task<SyncConflict?> RemoveConflictAsync(Guid operationId, Action<SQLiteConnection, SyncConflict> localChange)
    {
        ArgumentNullException.ThrowIfNull(localChange);

        SyncConflict? conflict = null;
        await Connection.RunInTransactionAsync(connection =>
        {
            conflict = TakeConflict(connection, operationId);
            if (conflict is not null)
            {
                localChange(connection, conflict);
            }
        });
        return conflict;
    }

    public async Task<IReadOnlyList<OutboxOperation>> RemoveWorkOrderChangesAsync(
        Guid workOrderId, Action<SQLiteConnection, IReadOnlyList<OutboxOperation>> localChange)
    {
        ArgumentNullException.ThrowIfNull(localChange);

        List<OutboxOperation> removed = [];
        await Connection.RunInTransactionAsync(connection =>
        {
            removed.AddRange(connection
                .Query<SyncConflictRow>("SELECT * FROM sync_conflicts WHERE work_order_id = ?", workOrderId)
                .Select(row => row.ToConflict().ToOperation()));
            removed.AddRange(connection
                .Query<OutboxOperationRow>("SELECT * FROM outbox_operations WHERE work_order_id = ?", workOrderId)
                .Select(row => row.ToOperation()));
            connection.Execute("DELETE FROM sync_conflicts WHERE work_order_id = ?", workOrderId);
            connection.Execute("DELETE FROM outbox_operations WHERE work_order_id = ?", workOrderId);
            localChange(connection, removed);
        });
        return removed;
    }

    public async Task<int> CountPendingAsync() => await Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM outbox_operations");

    public async Task<int> CountConflictsAsync() => await Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sync_conflicts");

    private static SyncConflict? TakeConflict(SQLiteConnection connection, Guid operationId)
    {
        var row = connection.Query<SyncConflictRow>("SELECT * FROM sync_conflicts WHERE operation_id = ?", operationId).FirstOrDefault();
        if (row is not null)
        {
            connection.Execute("DELETE FROM sync_conflicts WHERE operation_id = ?", operationId);
        }

        return row?.ToConflict();
    }
}
