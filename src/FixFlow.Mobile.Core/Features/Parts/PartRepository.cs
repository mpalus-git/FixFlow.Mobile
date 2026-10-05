using FixFlow.Mobile.Core.Storage;
using SQLite;

namespace FixFlow.Mobile.Core.Features.Parts;

public sealed class PartRepository(UserDatabase userDatabase)
{
    public const int DefaultSearchLimit = 50;

    private SQLiteAsyncConnection Connection => userDatabase.Current.Connection;

    public async Task ReplaceAllAsync(IReadOnlyCollection<Part> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var rows = parts.Select(PartRow.From).ToList();
        await Connection.RunInTransactionAsync(connection =>
        {
            connection.Execute("DELETE FROM parts");
            connection.InsertAll(rows, runInTransaction: false);
        });
    }

    public async Task<IReadOnlyList<Part>> SearchAsync(string term, int limit = DefaultSearchLimit)
    {
        ArgumentNullException.ThrowIfNull(term);

        var pattern = $"%{EscapeLike(PartRow.NormalizeForSearch(term))}%";
        var rows = await Connection.QueryAsync<PartRow>(
            "SELECT * FROM parts WHERE archived_at IS NULL AND search_text LIKE ? ESCAPE '\\' ORDER BY name LIMIT ?",
            pattern,
            limit);
        return [.. rows.Select(row => row.ToPart())];
    }

    public async Task<Part?> GetAsync(Guid id)
    {
        var rows = await Connection.QueryAsync<PartRow>("SELECT * FROM parts WHERE id = ?", id);
        return rows.FirstOrDefault()?.ToPart();
    }

    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
