using System.Windows.Input;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.Core.Screens;

namespace FixFlow.Mobile.Controls;

public sealed class StatusBanner : Border
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(StatusBanner), propertyChanged: OnTextChanged);

    public static readonly BindableProperty ToneProperty =
        BindableProperty.Create(nameof(Tone), typeof(StatusTone), typeof(StatusBanner), StatusTone.Neutral, propertyChanged: OnToneChanged);

    public static readonly BindableProperty HasActionProperty =
        BindableProperty.Create(nameof(HasAction), typeof(bool), typeof(StatusBanner), false, propertyChanged: OnHasActionChanged);

    public static readonly BindableProperty ActionCommandProperty =
        BindableProperty.Create(nameof(ActionCommand), typeof(ICommand), typeof(StatusBanner), propertyChanged: OnActionCommandChanged);

    private readonly Label _label = new() { FontSize = 14, VerticalOptions = LayoutOptions.Center };

    private readonly Button _action = new()
    {
        Text = AppResources.StatusBannerShowButton,
        FontSize = 14,
        Padding = new Thickness(16, 0),
        BorderWidth = 1,
        BackgroundColor = Colors.Transparent,
        VerticalOptions = LayoutOptions.Center,
        IsVisible = false,
    };

    public StatusBanner()
    {
        StrokeThickness = 0;
        Padding = new Thickness(16, 8);
        MinimumHeightRequest = 48;
        SemanticProperties.SetDescription(_action, AppResources.StatusBannerShowDescription);
        var layout = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12 };
        layout.Add(_label);
        layout.Add(_action, 1);
        Content = layout;
        ApplyTone();
    }

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public StatusTone Tone
    {
        get => (StatusTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public bool HasAction
    {
        get => (bool)GetValue(HasActionProperty);
        set => SetValue(HasActionProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    private static void OnTextChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((StatusBanner)bindable)._label.Text = (string?)newValue;

    private static void OnToneChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((StatusBanner)bindable).ApplyTone();

    private static void OnHasActionChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((StatusBanner)bindable)._action.IsVisible = (bool)newValue;

    private static void OnActionCommandChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((StatusBanner)bindable)._action.Command = (ICommand?)newValue;

    private void ApplyTone()
    {
        var (background, foreground) = Tone switch
        {
            StatusTone.Warning => ("BadgeWarning", "BadgeWarningForeground"),
            StatusTone.Destructive => ("BadgeDestructive", "DestructiveText"),
            _ => ("Muted", "Foreground"),
        };
        this.SetAppThemeColor(BackgroundColorProperty, ThemeColor(background, "Light"), ThemeColor(background, "Dark"));
        _label.SetAppThemeColor(Label.TextColorProperty, ThemeColor(foreground, "Light"), ThemeColor(foreground, "Dark"));
        _action.SetAppThemeColor(Button.TextColorProperty, ThemeColor(foreground, "Light"), ThemeColor(foreground, "Dark"));
        _action.SetAppThemeColor(Button.BorderColorProperty, ThemeColor(foreground, "Light"), ThemeColor(foreground, "Dark"));
    }

    private static Color ThemeColor(string name, string theme) =>
        Application.Current?.Resources.TryGetValue(name + theme, out var value) == true && value is Color color
            ? color
            : Colors.Transparent;
}
