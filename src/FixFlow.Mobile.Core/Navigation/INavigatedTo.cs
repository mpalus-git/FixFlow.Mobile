namespace FixFlow.Mobile.Core.Navigation;

public interface INavigatedTo<in TParameter>
{
    Task OnNavigatedToAsync(TParameter parameter);
}
