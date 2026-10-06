using FixFlow.Mobile.Core.Storage;
using SQLite;

namespace FixFlow.Mobile.UnitTests.Storage;

public sealed class LocalDatabaseTests : IDisposable
{
    private static readonly DatabaseMigration CreateNotes = new(1, "create_notes", ["CREATE TABLE notes (id INTEGER PRIMARY KEY, text TEXT NOT NULL)"]);
    private static readonly DatabaseMigration AddNoteAuthor = new(2, "add_note_author", ["ALTER TABLE notes ADD COLUMN author TEXT"]);

    private readonly TemporaryDatabaseFile _file = new();

    [Fact]
    public async Task Should_Apply_All_Migrations_When_Database_Is_New()
    {
        await using var database = await LocalDatabase.OpenAsync(_file.Path, [CreateNotes, AddNoteAuthor], TimeProvider.System);

        (await database.GetAppliedMigrationsAsync()).ShouldBe([1, 2]);
        await database.Connection.ExecuteAsync("INSERT INTO notes (text, author) VALUES ('first', 'Jan')");
    }

    [Fact]
    public async Task Should_Keep_Data_And_Not_Reapply_When_Database_Is_Reopened()
    {
        await using (var database = await LocalDatabase.OpenAsync(_file.Path, [CreateNotes], TimeProvider.System))
        {
            await database.Connection.ExecuteAsync("INSERT INTO notes (text) VALUES ('first')");
        }

        await using var reopened = await LocalDatabase.OpenAsync(_file.Path, [CreateNotes], TimeProvider.System);

        (await reopened.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM notes")).ShouldBe(1);
        (await reopened.GetAppliedMigrationsAsync()).ShouldBe([1]);
    }

    [Fact]
    public async Task Should_Apply_Only_Missing_Migration_When_Schema_Is_Older()
    {
        await using (var database = await LocalDatabase.OpenAsync(_file.Path, [CreateNotes], TimeProvider.System))
        {
            await database.Connection.ExecuteAsync("INSERT INTO notes (text) VALUES ('first')");
        }

        await using var upgraded = await LocalDatabase.OpenAsync(_file.Path, [CreateNotes, AddNoteAuthor], TimeProvider.System);

        (await upgraded.GetAppliedMigrationsAsync()).ShouldBe([1, 2]);
        (await upgraded.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM notes WHERE author IS NULL")).ShouldBe(1);
    }

    [Fact]
    public async Task Should_Not_Crash_Running_Queries_When_Database_Is_Closed()
    {
        for (var round = 0; round < 30; round++)
        {
            var database = await LocalDatabase.OpenAsync(_file.Path, [CreateNotes], TimeProvider.System);
            var queries = Enumerable.Range(0, 8)
                .Select(_ => database.Connection.QueryAsync<Note>("SELECT id, text FROM notes"))
                .ToList();

            await database.DisposeAsync();

            foreach (var query in queries)
            {
                try
                {
                    await query;
                }
                catch (Exception exception) when (exception is not AccessViolationException)
                {
                }
            }
        }
    }

    [Fact]
    public async Task Should_Roll_Back_Migration_When_One_Of_Its_Statements_Fails()
    {
        var broken = new DatabaseMigration(2, "broken", ["ALTER TABLE notes ADD COLUMN author TEXT", "NOT VALID SQL"]);
        await using (await LocalDatabase.OpenAsync(_file.Path, [CreateNotes], TimeProvider.System))
        {
        }

        await Should.ThrowAsync<SQLiteException>(() => LocalDatabase.OpenAsync(_file.Path, [CreateNotes, broken], TimeProvider.System));

        await using var database = await LocalDatabase.OpenAsync(_file.Path, [CreateNotes, AddNoteAuthor], TimeProvider.System);
        (await database.GetAppliedMigrationsAsync()).ShouldBe([1, 2]);
    }

    public void Dispose() => _file.Dispose();

    private sealed class Note
    {
        public int Id { get; set; }

        public string Text { get; set; } = string.Empty;
    }
}
