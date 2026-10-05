namespace FixFlow.Mobile.Controls;

public partial class EmptyView : ContentView
{
    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(EmptyView));

    public EmptyView()
    {
        InitializeComponent();
    }

    public string? Message
    {
        get => (string?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }
}
