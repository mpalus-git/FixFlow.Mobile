namespace FixFlow.Mobile.Core.Screens;

public sealed record ScreenNotice(string Message, StatusTone Tone)
{
    public static ScreenNotice Info(string message) => new(message, StatusTone.Neutral);

    public static ScreenNotice Problem(string message) => new(message, StatusTone.Warning);
}
