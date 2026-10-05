using FixFlow.Mobile.Core.Api.Errors;

namespace FixFlow.Mobile.Core.Features.Auth;

public sealed record LoginResult(LoginOutcome Outcome, SessionUser? User, ApiError? Error)
{
    public static LoginResult Succeeded(SessionUser user) => new(LoginOutcome.Succeeded, user, null);

    public static LoginResult NotTechnician() => new(LoginOutcome.NotTechnician, null, null);

    public static LoginResult Failed(ApiError error) => new(LoginOutcome.Failed, null, error);
}
