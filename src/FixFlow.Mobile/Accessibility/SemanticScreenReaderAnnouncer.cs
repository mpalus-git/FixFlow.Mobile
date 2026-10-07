using FixFlow.Mobile.Core.Accessibility;

namespace FixFlow.Mobile.Accessibility;

public sealed class SemanticScreenReaderAnnouncer(ISemanticScreenReader screenReader) : IScreenReaderAnnouncer
{
    public void Announce(string message) => screenReader.Announce(message);
}
