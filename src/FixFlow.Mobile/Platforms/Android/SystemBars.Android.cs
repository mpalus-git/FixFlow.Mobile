using AndroidX.Core.View;

namespace FixFlow.Mobile;

public static partial class SystemBars
{
    private static partial void ApplyPlatformTheme(Window window, AppTheme theme)
    {
        if (window.Handler?.PlatformView is not Android.App.Activity { Window: { } platformWindow }
            || WindowCompat.GetInsetsController(platformWindow, platformWindow.DecorView) is not { } controller)
        {
            return;
        }

        var useDarkIcons = theme != AppTheme.Dark;
        controller.AppearanceLightStatusBars = useDarkIcons;
        controller.AppearanceLightNavigationBars = useDarkIcons;
    }
}
