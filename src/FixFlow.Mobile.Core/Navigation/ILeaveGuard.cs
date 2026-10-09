namespace FixFlow.Mobile.Core.Navigation;

public interface ILeaveGuard
{
    Task<bool> CanLeaveAsync();
}
