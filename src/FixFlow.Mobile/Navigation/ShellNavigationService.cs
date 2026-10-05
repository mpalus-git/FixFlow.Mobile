using FixFlow.Mobile.Core.Navigation;

namespace FixFlow.Mobile.Navigation;

public sealed class ShellNavigationService : INavigationService
{
    public Task NavigateToAsync<TViewModel>()
        where TViewModel : class =>
        Shell.Current.GoToAsync(NavigationRoute.For<TViewModel>());

    public async Task NavigateToAsync<TViewModel, TParameter>(TParameter parameter)
        where TViewModel : class, INavigatedTo<TParameter>
    {
        await Shell.Current.GoToAsync(NavigationRoute.For<TViewModel>());

        if (Shell.Current.CurrentPage.BindingContext is INavigatedTo<TParameter> target)
        {
            await target.OnNavigatedToAsync(parameter);
        }
    }

    public Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
