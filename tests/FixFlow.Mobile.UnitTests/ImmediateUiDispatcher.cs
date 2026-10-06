using System.Collections.Concurrent;
using FixFlow.Mobile.Core.Dispatching;

namespace FixFlow.Mobile.UnitTests;

internal sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public ConcurrentQueue<Task> Posted { get; } = new();

    public void Post(Func<Task> action) => Posted.Enqueue(action());
}
