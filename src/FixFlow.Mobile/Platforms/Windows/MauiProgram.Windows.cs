using Microsoft.Maui.Handlers;

namespace FixFlow.Mobile;

public static partial class MauiProgram
{
    private static partial void ConfigurePlatformHandlers(IMauiHandlersCollection handlers) =>
        EntryHandler.Mapper.AppendToMapping("BorderlessEntry", (handler, _) =>
        {
            var textBox = handler.PlatformView;
            var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            textBox.Resources["TextControlBorderThemeThickness"] = new Microsoft.UI.Xaml.Thickness(0);
            textBox.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
            textBox.Resources["TextControlBackground"] = transparent;
            textBox.Resources["TextControlBackgroundPointerOver"] = transparent;
            textBox.Resources["TextControlBackgroundFocused"] = transparent;
        });
}
