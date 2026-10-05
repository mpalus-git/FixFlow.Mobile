using System.Net;

namespace FixFlow.Mobile.Core.Api.Errors;

public sealed record ApiError(
    ApiErrorKind Kind,
    HttpStatusCode? StatusCode,
    string? ErrorCode,
    string? Detail,
    IReadOnlyDictionary<string, IReadOnlyList<string>> FieldErrors,
    TimeSpan? RetryAfter)
{
    public bool ShouldRetry => Kind == ApiErrorKind.Transient;

    public bool IsConflict => Kind is ApiErrorKind.Business or ApiErrorKind.Validation;

    public static ApiError Transient() =>
        new(ApiErrorKind.Transient, null, null, null, new Dictionary<string, IReadOnlyList<string>>(), null);
}
