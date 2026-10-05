using System.Globalization;
using SQLite;

namespace FixFlow.Mobile.Core.Storage;

public sealed class LocalDatabase : IAsyncDisposable
{
    private const string CreateMigrationsTable =
        "CREATE TABLE IF NOT EXISTS schema_migrations (version INTEGER PRIMARY KEY, name TEXT NOT NULL, applied_at TEXT NOT NULL)";

    private LocalDatabase(SQLiteAsyncConnection connection)
    {
        Connection = connection;
    }

    public SQLiteAsyncConnection Connection { get; }

    public static async Task<LocalDatabase> OpenAsync(
        string path,
        IReadOnlyList<DatabaseMigration> migrations,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var connection = new SQLiteAsyncConnection(
            path,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
        try
        {
            await MigrateAsync(connection, migrations, timeProvider);
            return new LocalDatabase(connection);
        }
        catch
        {
            await connection.CloseAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<int>> GetAppliedMigrationsAsync() =>
        await Connection.QueryScalarsAsync<int>("SELECT version FROM schema_migrations ORDER BY version");

    public async ValueTask DisposeAsync() => await Connection.CloseAsync();

    private static async Task MigrateAsync(
        SQLiteAsyncConnection connection,
        IReadOnlyList<DatabaseMigration> migrations,
        TimeProvider timeProvider)
    {
        await connection.ExecuteAsync(CreateMigrationsTable);
        var applied = (await connection.QueryScalarsAsync<int>("SELECT version FROM schema_migrations")).ToHashSet();

        foreach (var migration in migrations.Where(migration => !applied.Contains(migration.Version)).OrderBy(migration => migration.Version))
        {
            var appliedAt = timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
            await connection.RunInTransactionAsync(transaction =>
            {
                foreach (var statement in migration.Statements)
                {
                    transaction.Execute(statement);
                }

                transaction.Execute(
                    "INSERT INTO schema_migrations (version, name, applied_at) VALUES (?, ?, ?)",
                    migration.Version,
                    migration.Name,
                    appliedAt);
            });
        }
    }
}
