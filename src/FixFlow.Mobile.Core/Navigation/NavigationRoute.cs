namespace FixFlow.Mobile.Core.Navigation;

public static class NavigationRoute
{
    private const string ViewModelSuffix = "ViewModel";

    public static string For<TViewModel>() => For(typeof(TViewModel));

    public static string For(Type viewModelType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);

        var name = viewModelType.Name;
        if (name.Length <= ViewModelSuffix.Length || !name.EndsWith(ViewModelSuffix, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Type '{name}' is not a view model.", nameof(viewModelType));
        }

        return name[..^ViewModelSuffix.Length];
    }
}
