namespace FixFlow.Mobile.UnitTests.Storage;

internal sealed class TemporaryDatabaseFile : IDisposable
{
    public string Directory { get; } = System.IO.Directory.CreateTempSubdirectory("fixflow-db-").FullName;

    public string Path => System.IO.Path.Combine(Directory, "test.db3");

    public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
}
