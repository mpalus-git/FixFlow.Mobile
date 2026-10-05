using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Refit;

namespace FixFlow.Mobile.Core.Api.Errors;

public static class ApiErrorMapper
{
    private static readonly HashSet<string> ProblemMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/problem+json", "application/json",
    };

    public static ApiError FromResponse(IApiResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response switch
        {
            { Error: ApiException error } => FromApiException(error),
            { Error: ApiRequestException error } => ApiError.Transient(error.InnerException?.Message ?? error.Message),
            { StatusCode: { } statusCode } => Map(statusCode, response.Headers, response.ContentHeaders, null),
            _ => ApiError.Transient(),
        };
    }

    public static ApiError? FromException(Exception exception) => exception switch
    {
        ApiException apiException => FromApiException(apiException),
        ApiRequestException => ApiError.Transient(exception.InnerException?.Message ?? exception.Message),
        HttpRequestException => ApiError.Transient(exception.Message),
        TaskCanceledException { InnerException: TimeoutException } => ApiError.Transient(exception.Message),
        _ => null,
    };

    private static ApiError FromApiException(ApiException exception) =>
        Map(exception.StatusCode, exception.Headers, exception.ContentHeaders, exception.Content);

    private static ApiError Map(
        HttpStatusCode statusCode,
        HttpResponseHeaders? headers,
        HttpContentHeaders? contentHeaders,
        string? content)
    {
        var problem = ParseProblem(contentHeaders, content);
        var fieldErrors = problem?.FieldErrors ?? new Dictionary<string, IReadOnlyList<string>>();

        return new ApiError(
            Classify(statusCode, fieldErrors.Count > 0),
            statusCode,
            problem?.ErrorCode,
            problem?.Detail,
            fieldErrors,
            headers?.RetryAfter?.Delta);
    }

    private static ApiErrorKind Classify(HttpStatusCode statusCode, bool hasFieldErrors) => (int)statusCode switch
    {
        401 => ApiErrorKind.Unauthorized,
        408 or 429 or >= 500 => ApiErrorKind.Transient,
        400 when hasFieldErrors => ApiErrorKind.Validation,
        _ => ApiErrorKind.Business,
    };

    private static ParsedProblem? ParseProblem(HttpContentHeaders? contentHeaders, string? content)
    {
        if (string.IsNullOrWhiteSpace(content)
            || contentHeaders?.ContentType?.MediaType is not { } mediaType
            || !ProblemMediaTypes.Contains(mediaType))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (document.RootElement.TryGetProperty("errors", out _))
            {
                var validation = document.RootElement.Deserialize<HttpValidationProblemDetails>();
                return new ParsedProblem(null, validation?.Detail, ToFieldErrors(validation?.Errors));
            }

            var problem = document.RootElement.Deserialize<ProblemDetails>();
            return new ParsedProblem(problem?.ErrorCode, problem?.Detail, null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Dictionary<string, IReadOnlyList<string>> ToFieldErrors(IDictionary<string, ICollection<string>>? errors) =>
        errors?.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)[.. pair.Value])
        ?? [];

    private sealed record ParsedProblem(
        string? ErrorCode,
        string? Detail,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? FieldErrors);
}
