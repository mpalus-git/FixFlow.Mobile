namespace FixFlow.Mobile;

public static partial class MauiProgram
{
    private static partial void ConfigurePlatformHandlers(IMauiHandlersCollection handlers) =>
        handlers.AddHandler<Shell, EdgeToEdgeShellRenderer>();
}
