using FixFlow.Mobile.Core.Features.Settings;

namespace FixFlow.Mobile.Features.Settings;

public sealed class EssentialsAppVersion(IAppInfo appInfo) : IAppVersion
{
    public string Version => appInfo.VersionString;

    public string Build => appInfo.BuildString;
}
