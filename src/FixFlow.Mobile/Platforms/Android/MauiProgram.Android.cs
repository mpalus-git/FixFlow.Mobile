using Android.Content.Res;
using AndroidX.AppCompat.Widget;
using FixFlow.Mobile.Core.Notifications;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Handlers;
using Plugin.LocalNotification;

namespace FixFlow.Mobile;

public static partial class MauiProgram
{
    private static partial void ConfigurePlatformServices(MauiAppBuilder builder)
    {
        builder.UseLocalNotification();
        builder.Services.AddSingleton<INotificationScheduler, LocalNotificationScheduler>();
    }

    private static partial void ConfigurePlatformHandlers(IMauiHandlersCollection handlers)
    {
        handlers.AddHandler<Shell, EdgeToEdgeShellRenderer>();
        EntryHandler.Mapper.AppendToMapping("BorderlessEntry", (handler, _) => handler.PlatformView.Background = null);
        EditorHandler.Mapper.AppendToMapping("BorderlessEditor", (handler, _) => handler.PlatformView.Background = null);
        DatePickerHandler.Mapper.AppendToMapping("BorderlessDatePicker", (handler, _) => handler.PlatformView.Background = null);
        TimePickerHandler.Mapper.AppendToMapping("BorderlessTimePicker", (handler, _) => handler.PlatformView.Background = null);
        RadioButtonHandler.Mapper.AppendToMapping(nameof(ITextStyle.TextColor), (handler, view) =>
        {
            if (handler.PlatformView is AppCompatRadioButton radioButton && view.TextColor is { } color)
            {
                radioButton.ButtonTintList = ColorStateList.ValueOf(color.ToPlatform());
            }
        });
    }

    private static partial void ConfigurePlatformImageSources(IImageSourceServiceCollection services)
    {
    }
}
