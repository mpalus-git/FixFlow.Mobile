namespace FixFlow.Mobile.Core.Navigation;

public interface INavigationService
{
    Task NavigateToAsync<TViewModel>()
        where TViewModel : class;

    Task NavigateToAsync<TViewModel, TParameter>(TParameter parameter)
        where TViewModel : class, INavigatedTo<TParameter>;

    Task NavigateToRootAsync<TViewModel>()
        where TViewModel : class;

    Task GoBackAsync();
}
