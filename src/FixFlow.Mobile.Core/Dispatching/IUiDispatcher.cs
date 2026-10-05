namespace FixFlow.Mobile.Core.Dispatching;

public interface IUiDispatcher
{
    void Post(Func<Task> action);
}
