using FixFlow.Mobile.Core.Dispatching;

namespace FixFlow.Mobile.Dispatching;

public sealed class MainThreadUiDispatcher : IUiDispatcher
{
    public void Post(Func<Task> action) => MainThread.BeginInvokeOnMainThread(async () => await action());
}
