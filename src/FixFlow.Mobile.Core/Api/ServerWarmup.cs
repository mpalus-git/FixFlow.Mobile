namespace FixFlow.Mobile.Core.Api;

public sealed class ServerWarmup
{
    public bool IsServerReady { get; private set; }

    public void MarkReady() => IsServerReady = true;
}
