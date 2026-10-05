namespace FixFlow.Mobile.Core.Launching;

public interface IUriLauncher
{
    Task OpenAsync(Uri uri);
}
