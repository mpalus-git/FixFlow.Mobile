using FixFlow.Mobile.Core.Dispatching;

namespace FixFlow.Mobile.UnitTests;

internal sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public void Post(Func<Task> action) => _ = action();
}
