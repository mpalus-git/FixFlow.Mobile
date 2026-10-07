using FixFlow.Mobile.Core.Notifications;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace FixFlow.Mobile;

public static partial class MauiProgram
{
    private static readonly string[] RadioButtonCheckedStates = [string.Empty, "PointerOver", "Pressed"];

    private static partial void ConfigurePlatformServices(MauiAppBuilder builder) =>
        builder.Services.AddSingleton<INotificationScheduler, DisabledNotificationScheduler>();

    private static partial void ConfigurePlatformHandlers(IMauiHandlersCollection handlers)
    {
        EntryHandler.Mapper.AppendToMapping("BorderlessEntry", (handler, _) => RemoveTextBoxChrome(handler.PlatformView));
        EditorHandler.Mapper.AppendToMapping("BorderlessEditor", (handler, _) => RemoveTextBoxChrome(handler.PlatformView));
        DatePickerHandler.Mapper.AppendToMapping("BorderlessDatePicker", (handler, _) => RemoveControlChrome(handler.PlatformView));
        TimePickerHandler.Mapper.AppendToMapping("BorderlessTimePicker", (handler, _) => RemoveControlChrome(handler.PlatformView));
        RadioButtonHandler.Mapper.AppendToMapping(nameof(ITextStyle.TextColor), (handler, view) =>
        {
            if (view.TextColor is { } color)
            {
                ApplyRadioButtonColor(handler.PlatformView, color);
            }
        });
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

    private static void ApplyRadioButtonColor(Microsoft.UI.Xaml.Controls.RadioButton radioButton, Color color)
    {
        var mark = new Microsoft.UI.Xaml.Media.SolidColorBrush(color.ToWindowsColor());
        var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        foreach (var state in RadioButtonCheckedStates)
        {
            radioButton.Resources["RadioButtonOuterEllipseCheckedStroke" + state] = mark;
            radioButton.Resources["RadioButtonOuterEllipseCheckedFill" + state] = transparent;
            radioButton.Resources["RadioButtonCheckGlyphFill" + state] = mark;
        }
    }

    private static void RemoveControlChrome(Microsoft.UI.Xaml.Controls.Control control)
    {
        control.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
        control.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }
}
