namespace FixFlow.Mobile.Core.Launching;

public interface IUriLauncher
{
    Task<bool> OpenAsync(Uri uri);
}
