using FixFlow.Mobile.Core.Storage;

namespace FixFlow.Mobile.UnitTests.Storage;

public sealed class UserDatabaseTests : IAsyncDisposable
{
    private static readonly Guid TechnicianA = Guid.NewGuid();
    private static readonly Guid TechnicianB = Guid.NewGuid();

    private readonly TemporaryDatabaseFile _file = new();
    private readonly UserDatabase _userDatabase;

    public UserDatabaseTests()
    {
        _userDatabase = new UserDatabase(new LocalDatabaseOptions(_file.Directory), TimeProvider.System);
    }

    [Fact]
    public async Task Should_Not_Show_Data_Of_Other_Technician_When_User_Changes()
    {
        await _userDatabase.OpenAsync(TechnicianA);
        await _userDatabase.Current.Connection.ExecuteAsync("CREATE TABLE notes (text TEXT NOT NULL)");
        await _userDatabase.Current.Connection.ExecuteAsync("INSERT INTO notes (text) VALUES ('technician A')");

        await _userDatabase.OpenAsync(TechnicianB);

        _userDatabase.UserId.ShouldBe(TechnicianB);
        (await _userDatabase.Current.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'notes'")).ShouldBe(0);
    }

    [Fact]
    public async Task Should_Restore_Data_When_Same_Technician_Logs_In_Again()
    {
        await _userDatabase.OpenAsync(TechnicianA);
        await _userDatabase.Current.Connection.ExecuteAsync("CREATE TABLE notes (text TEXT NOT NULL)");
        await _userDatabase.Current.Connection.ExecuteAsync("INSERT INTO notes (text) VALUES ('technician A')");
        await _userDatabase.CloseAsync();

        await _userDatabase.OpenAsync(TechnicianA);

        (await _userDatabase.Current.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM notes")).ShouldBe(1);
    }

    [Fact]
    public async Task Should_Keep_Open_Database_When_Same_Technician_Is_Opened_Twice()
    {
        await _userDatabase.OpenAsync(TechnicianA);
        var first = _userDatabase.Current;

        await _userDatabase.OpenAsync(TechnicianA);

        _userDatabase.Current.ShouldBeSameAs(first);
        File.Exists(_userDatabase.PathFor(TechnicianA)).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Refuse_Access_When_No_Database_Is_Open()
    {
        await _userDatabase.OpenAsync(TechnicianA);
        await _userDatabase.CloseAsync();

        Should.Throw<InvalidOperationException>(() => _userDatabase.Current);
    }

    [Fact]
    public async Task Should_Delete_Only_Current_Technician_Files_When_Database_Is_Deleted()
    {
        await _userDatabase.OpenAsync(TechnicianB);
        await _userDatabase.OpenAsync(TechnicianA);
        await _userDatabase.Current.Connection.ExecuteAsync("CREATE TABLE notes (text TEXT NOT NULL)");

        await _userDatabase.DeleteAsync();

        _userDatabase.UserId.ShouldBeNull();
        Directory.GetFiles(_file.Directory, $"fixflow-{TechnicianA:N}.db3*").ShouldBeEmpty();
        File.Exists(_userDatabase.PathFor(TechnicianB)).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Start_With_Empty_Database_When_Technician_Logs_In_After_Deletion()
    {
        await _userDatabase.OpenAsync(TechnicianA);
        await _userDatabase.Current.Connection.ExecuteAsync("CREATE TABLE notes (text TEXT NOT NULL)");
        await _userDatabase.DeleteAsync();

        await _userDatabase.OpenAsync(TechnicianA);

        (await _userDatabase.Current.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'notes'")).ShouldBe(0);
    }

    public async ValueTask DisposeAsync()
    {
        await _userDatabase.DisposeAsync();
        _file.Dispose();
    }
}
