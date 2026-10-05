namespace FixFlow.Mobile.Core.Storage;

public sealed record DatabaseMigration(int Version, string Name, IReadOnlyList<string> Statements);
