using FixFlow.Mobile.Core.Notifications;
using Microsoft.Maui.Handlers;

namespace FixFlow.Mobile;

public static partial class MauiProgram
{
    private static partial void ConfigurePlatformServices(MauiAppBuilder builder) =>
        builder.Services.AddSingleton<INotificationScheduler, DisabledNotificationScheduler>();

    private static partial void ConfigurePlatformHandlers(IMauiHandlersCollection handlers)
    {
        EntryHandler.Mapper.AppendToMapping("BorderlessEntry", (handler, _) => RemoveTextBoxChrome(handler.PlatformView));
        EditorHandler.Mapper.AppendToMapping("BorderlessEditor", (handler, _) => RemoveTextBoxChrome(handler.PlatformView));
        DatePickerHandler.Mapper.AppendToMapping("BorderlessDatePicker", (handler, _) => RemoveControlChrome(handler.PlatformView));
        TimePickerHandler.Mapper.AppendToMapping("BorderlessTimePicker", (handler, _) => RemoveControlChrome(handler.PlatformView));
    }

    private static partial void ConfigurePlatformImageSources(IImageSourceServiceCollection services) =>
        services.AddService<UriImageSource, UserAgentUriImageSourceService>();

    private static void RemoveTextBoxChrome(Microsoft.UI.Xaml.Controls.TextBox textBox)
    {
        var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        textBox.Resources["TextControlBorderThemeThickness"] = new Microsoft.UI.Xaml.Thickness(0);
        textBox.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
        textBox.Resources["TextControlBackground"] = transparent;
        textBox.Resources["TextControlBackgroundPointerOver"] = transparent;
        textBox.Resources["TextControlBackgroundFocused"] = transparent;
    }

    private static void RemoveControlChrome(Microsoft.UI.Xaml.Controls.Control control)
    {
        control.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
        control.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }
}
