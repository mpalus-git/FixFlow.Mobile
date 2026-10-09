using FixFlow.Mobile.Core.Features.WorkOrders;
using Microsoft.Maui.Controls.Shapes;

namespace FixFlow.Mobile.Controls;

public sealed class Badge : Border
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(Badge), propertyChanged: OnTextChanged);

    public static readonly BindableProperty ToneProperty =
        BindableProperty.Create(nameof(Tone), typeof(BadgeTone), typeof(Badge), BadgeTone.Neutral, propertyChanged: OnToneChanged);

    private readonly Label _label = new() { FontSize = 12 };

    public Badge()
    {
        StrokeShape = new RoundRectangle { CornerRadius = 10 };
        StrokeThickness = 1;
        Padding = new Thickness(8, 2);
        VerticalOptions = LayoutOptions.Center;
        HorizontalOptions = LayoutOptions.End;
        Content = _label;
        ApplyTone();
    }

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public BadgeTone Tone
    {
        get => (BadgeTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    private static void OnTextChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((Badge)bindable)._label.Text = (string?)newValue;

    private static void OnToneChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((Badge)bindable).ApplyTone();

    private void ApplyTone()
    {
        var (background, foreground, stroke) = Tone switch
        {
            BadgeTone.Outline => (null, "MutedForeground", "Input"),
            BadgeTone.Info => ("BadgeInfo", "BadgeInfoForeground", null),
            BadgeTone.Warning => ("BadgeWarning", "BadgeWarningForeground", null),
            BadgeTone.Success => ("BadgeSuccess", "BadgeSuccessForeground", null),
            BadgeTone.Caution => ("BadgeCaution", "BadgeCautionForeground", null),
            BadgeTone.Destructive => ("BadgeDestructive", "DestructiveText", null),
            _ => ("Muted", "Foreground", (string?)null),
        };
        this.SetAppThemeColor(BackgroundColorProperty, ThemeColor(background, "Light"), ThemeColor(background, "Dark"));
        this.SetAppTheme<Brush>(StrokeProperty, new SolidColorBrush(ThemeColor(stroke, "Light")), new SolidColorBrush(ThemeColor(stroke, "Dark")));
        _label.SetAppThemeColor(Label.TextColorProperty, ThemeColor(foreground, "Light"), ThemeColor(foreground, "Dark"));
    }

    private static Color ThemeColor(string? name, string theme) =>
        name is not null && Application.Current?.Resources.TryGetValue(name + theme, out var value) == true && value is Color color
            ? color
            : Colors.Transparent;
}
