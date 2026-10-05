namespace FixFlow.Mobile.Core.Features.Auth;

public sealed class SessionEndedEventArgs(SessionEndReason reason) : EventArgs
{
    public SessionEndReason Reason { get; } = reason;
}
