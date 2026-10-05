using FixFlow.Mobile.Core.Api.Errors;

namespace FixFlow.Mobile.Core.Features.Sync;

public sealed record PullResult(PullOutcome Outcome, ApiError? Error)
{
    public static PullResult Succeeded { get; } = new(PullOutcome.Succeeded, null);

    public static PullResult Failed(ApiError error) => new(PullOutcome.Failed, error);
}
