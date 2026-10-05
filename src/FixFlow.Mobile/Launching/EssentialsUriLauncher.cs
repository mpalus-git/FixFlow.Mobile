using FixFlow.Mobile.Core.Launching;

namespace FixFlow.Mobile.Launching;

public sealed class EssentialsUriLauncher(ILauncher launcher) : IUriLauncher
{
    public Task OpenAsync(Uri uri) => launcher.OpenAsync(uri);
}
