using System.Globalization;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.UnitTests;

public sealed class GlobalCultureScope : IDisposable
{
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

    public void Dispose()
    {
        CultureInfo.DefaultThreadCurrentCulture = null;
        CultureInfo.DefaultThreadCurrentUICulture = null;
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUiCulture;
        AppResources.Culture = null;
    }
}
