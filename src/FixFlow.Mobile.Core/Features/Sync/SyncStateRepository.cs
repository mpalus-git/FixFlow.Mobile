using FixFlow.Mobile.Core.Storage;
using SQLite;

namespace FixFlow.Mobile.Core.Features.Sync;

public sealed class SyncStateRepository(UserDatabase userDatabase, TimeProvider timeProvider)
{
    private SQLiteAsyncConnection Connection => userDatabase.Current.Connection;

    public async Task<SyncState?> GetAsync(string key)
    {
        var rows = await Connection.QueryAsync<SyncStateRow>("SELECT * FROM sync_states WHERE key = ?", key);
        return rows.FirstOrDefault() is { } row ? new SyncState(row.Key, row.Value, row.UpdatedAt) : null;
    }

    public async Task SetAsync(string key, string value) =>
        await Connection.InsertOrReplaceAsync(new SyncStateRow { Key = key, Value = value, UpdatedAt = timeProvider.GetUtcNow() });
}
