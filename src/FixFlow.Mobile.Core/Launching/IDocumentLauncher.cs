namespace FixFlow.Mobile.Core.Launching;

public interface IDocumentLauncher
{
    Task OpenAsync(Stream content, string fileName, string contentType);
}
