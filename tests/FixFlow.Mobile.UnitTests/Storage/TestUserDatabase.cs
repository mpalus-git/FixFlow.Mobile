using FixFlow.Mobile.Core.Storage;

namespace FixFlow.Mobile.UnitTests.Storage;

internal sealed class TestUserDatabase : IAsyncDisposable
{
    private readonly TemporaryDatabaseFile _file = new();

    public TestUserDatabase(TimeProvider? timeProvider = null)
    {
        Options = new LocalDatabaseOptions(_file.Directory);
        Database = new UserDatabase(Options, timeProvider ?? TimeProvider.System);
    }

    public LocalDatabaseOptions Options { get; }

    public UserDatabase Database { get; }

    public async ValueTask DisposeAsync()
    {
        await Database.DisposeAsync();
        _file.Dispose();
    }
}
