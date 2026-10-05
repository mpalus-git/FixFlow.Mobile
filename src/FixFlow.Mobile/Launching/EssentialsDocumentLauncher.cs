using FixFlow.Mobile.Core.Launching;

namespace FixFlow.Mobile.Launching;

public sealed class EssentialsDocumentLauncher(ILauncher launcher, IFileSystem fileSystem) : IDocumentLauncher
{
    public async Task OpenAsync(Stream content, string fileName, string contentType)
    {
        var path = Path.Combine(fileSystem.CacheDirectory, Path.GetFileName(fileName));
        await using (var file = File.Create(path))
        {
            await content.CopyToAsync(file);
        }

        await launcher.OpenAsync(new OpenFileRequest(fileName, new ReadOnlyFile(path, contentType)));
    }
}
