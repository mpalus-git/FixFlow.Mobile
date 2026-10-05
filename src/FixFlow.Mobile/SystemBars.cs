namespace FixFlow.Mobile;

public static partial class SystemBars
{
    public static void ApplyTheme(Window window, AppTheme theme) => ApplyPlatformTheme(window, theme);

    private static partial void ApplyPlatformTheme(Window window, AppTheme theme);
}
