using Google.Android.Material.AppBar;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;
using AndroidToolbar = AndroidX.AppCompat.Widget.Toolbar;

namespace FixFlow.Mobile;

public sealed class EdgeToEdgeShellRenderer : ShellRenderer
{
    protected override IShellToolbarAppearanceTracker CreateToolbarAppearanceTracker() =>
        new StatusBarAreaAppearanceTracker(this);

    private sealed class StatusBarAreaAppearanceTracker(IShellContext shellContext)
        : ShellToolbarAppearanceTracker(shellContext)
    {
        public override void SetAppearance(AndroidToolbar toolbar, IShellToolbarTracker toolbarTracker, ShellAppearance appearance)
        {
            base.SetAppearance(toolbar, toolbarTracker, appearance);

            if (toolbar.Parent is AppBarLayout appBar && appearance.BackgroundColor is { } background)
            {
                appBar.SetBackgroundColor(background.ToPlatform());
            }
        }
    }
}
