using FixFlow.Mobile.Core.Storage;

namespace FixFlow.Mobile.UnitTests.Storage;

internal sealed class TestUserDatabase : IAsyncDisposable
{
    private readonly TemporaryDatabaseFile _file = new();

    public TestUserDatabase(TimeProvider? timeProvider = null)
    {
        Database = new UserDatabase(new LocalDatabaseOptions(_file.Directory), timeProvider ?? TimeProvider.System);
    }

    public UserDatabase Database { get; }

    public async ValueTask DisposeAsync()
    {
        await Database.DisposeAsync();
        _file.Dispose();
    }
}
