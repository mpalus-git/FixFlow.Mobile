using System.Globalization;
using System.Text;

namespace FixFlow.Mobile.Core.Resources;

public static class AppText
{
    public static string Format(string pattern, params object[] values) =>
        string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(pattern), values);
}
