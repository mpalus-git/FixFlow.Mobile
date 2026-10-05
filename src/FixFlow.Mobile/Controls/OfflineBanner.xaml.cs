namespace FixFlow.Mobile.Controls;

public partial class OfflineBanner : ContentView
{
    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(OfflineBanner));

    public OfflineBanner()
    {
        InitializeComponent();
    }

    public string? Message
    {
        get => (string?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }
}
