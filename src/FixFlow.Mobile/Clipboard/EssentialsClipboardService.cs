using FixFlow.Mobile.Core.Clipboard;

namespace FixFlow.Mobile.Clipboard;

public sealed class EssentialsClipboardService(IClipboard clipboard) : IClipboardService
{
    public Task SetTextAsync(string text) => clipboard.SetTextAsync(text);
}
