using FixFlow.Mobile.Core.Features.Auth;

namespace FixFlow.Mobile.Features.Auth;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
