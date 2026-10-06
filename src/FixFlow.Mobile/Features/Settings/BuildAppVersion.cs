using FixFlow.Mobile.Core.Features.Settings;

namespace FixFlow.Mobile.Features.Settings;

public sealed class BuildAppVersion : IAppVersion
{
    public string Version => BuildConfiguration.ApplicationDisplayVersion;

    public string Build => BuildConfiguration.ApplicationVersion;
}
