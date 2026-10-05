namespace FixFlow.Mobile.Core.Features.Auth;

public sealed record DemoAccount(string Email, string Password)
{
    public bool IsAvailable => !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrEmpty(Password);
}
