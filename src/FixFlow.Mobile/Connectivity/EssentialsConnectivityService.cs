using FixFlow.Mobile.Core.Connectivity;

namespace FixFlow.Mobile.Connectivity;

public sealed class EssentialsConnectivityService(IConnectivity connectivity) : IConnectivityService
{
    public bool IsOnline => connectivity.NetworkAccess == NetworkAccess.Internet;
}
