namespace FixFlow.Mobile.Core.Storage;

public sealed class UserDatabase(LocalDatabaseOptions options, TimeProvider timeProvider) : IAsyncDisposable
{
    private LocalDatabase? _database;

    public Guid? UserId { get; private set; }

    public LocalDatabase Current => _database ?? throw new InvalidOperationException("No user database is open.");

    public string PathFor(Guid userId) => Path.Combine(options.Directory, $"fixflow-{userId:N}.db3");

    public async Task OpenAsync(Guid userId)
    {
        if (_database is not null && UserId == userId)
        {
            return;
        }

        await CloseAsync();
        _database = await LocalDatabase.OpenAsync(PathFor(userId), DatabaseMigrations.All, timeProvider);
        UserId = userId;
    }

    public async Task CloseAsync()
    {
        if (_database is { } database)
        {
            _database = null;
            UserId = null;
            await database.DisposeAsync();
        }
    }

    public async Task DeleteAsync()
    {
        if (UserId is not { } userId)
        {
            return;
        }

        await CloseAsync();
        var path = PathFor(userId);
        foreach (var file in new[] { path, $"{path}-wal", $"{path}-shm", $"{path}-journal" })
        {
            File.Delete(file);
        }
    }

    public async ValueTask DisposeAsync() => await CloseAsync();
}
