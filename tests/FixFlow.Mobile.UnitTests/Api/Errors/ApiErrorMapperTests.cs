using System.Net;
using System.Text;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.UnitTests.Api;
using Refit;

namespace FixFlow.Mobile.UnitTests.Api.Errors;

public sealed class ApiErrorMapperTests
{
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task Should_Map_Error_Code_And_Detail_When_Api_Returns_Problem_Details()
    {
        var error = await StartWorkAsync(Respond(
            HttpStatusCode.Conflict,
            """{"status":409,"detail":"Technician already has a work order in progress.","errorCode":"WorkOrder.TechnicianHasWorkInProgress"}""",
            ProblemJson));

        error.Kind.ShouldBe(ApiErrorKind.Business);
        error.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        error.ErrorCode.ShouldBe("WorkOrder.TechnicianHasWorkInProgress");
        error.Detail.ShouldBe("Technician already has a work order in progress.");
        error.IsConflict.ShouldBeTrue();
        error.ShouldRetry.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Map_Field_Errors_When_Api_Returns_Validation_Problem()
    {
        var error = await StartWorkAsync(Respond(
            HttpStatusCode.BadRequest,
            """{"status":400,"title":"One or more validation errors occurred.","errors":{"note":["Note must not be empty."],"parts[0].quantity":["Quantity must be greater than 0."]}}""",
            ProblemJson));

        error.Kind.ShouldBe(ApiErrorKind.Validation);
        error.ErrorCode.ShouldBeNull();
        error.FieldErrors["note"].ShouldBe(["Note must not be empty."]);
        error.FieldErrors["parts[0].quantity"].ShouldBe(["Quantity must be greater than 0."]);
        error.IsConflict.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Read_Retry_After_When_Api_Returns_429()
    {
        var response = Respond(
            HttpStatusCode.TooManyRequests,
            """{"status":429,"detail":"Too many authentication requests. Try again later."}""",
            ProblemJson);
        response.Headers.Add("Retry-After", "42");

        var error = await StartWorkAsync(response);

        error.Kind.ShouldBe(ApiErrorKind.Transient);
        error.RetryAfter.ShouldBe(TimeSpan.FromSeconds(42));
        error.ShouldRetry.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Retry_When_Server_Returns_503_With_Html_Page()
    {
        var error = await StartWorkAsync(Respond(
            HttpStatusCode.ServiceUnavailable,
            "<html><body>Service waking up</body></html>",
            "text/html"));

        error.Kind.ShouldBe(ApiErrorKind.Transient);
        error.ErrorCode.ShouldBeNull();
        error.Detail.ShouldBeNull();
        error.ShouldRetry.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Move_Operation_To_Conflicts_When_Api_Returns_404()
    {
        var error = await StartWorkAsync(Respond(
            HttpStatusCode.NotFound,
            """{"status":404,"detail":"Work order was not found.","errorCode":"WorkOrder.NotFound"}""",
            ProblemJson));

        error.Kind.ShouldBe(ApiErrorKind.Business);
        error.ErrorCode.ShouldBe("WorkOrder.NotFound");
        error.IsConflict.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Classify_As_Unauthorized_When_Api_Returns_401_Without_Body()
    {
        var error = await StartWorkAsync(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        error.Kind.ShouldBe(ApiErrorKind.Unauthorized);
        error.IsConflict.ShouldBeFalse();
        error.ShouldRetry.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Classify_As_Business_When_Problem_Body_Is_Not_Valid_Json()
    {
        var error = await StartWorkAsync(Respond(HttpStatusCode.Conflict, "{not json", ProblemJson));

        error.Kind.ShouldBe(ApiErrorKind.Business);
        error.ErrorCode.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Retry_When_Request_Fails_With_Network_Error()
    {
        using var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("No route to host"));

        var error = await StartWorkAsync(handler);

        error.Kind.ShouldBe(ApiErrorKind.Transient);
        error.StatusCode.ShouldBeNull();
        error.Detail.ShouldBe("No route to host");
    }

    [Fact]
    public void Should_Retry_When_Http_Client_Times_Out()
    {
        var error = ApiErrorMapper.FromException(new TaskCanceledException("Timeout", new TimeoutException()));

        error.ShouldNotBeNull().ShouldRetry.ShouldBeTrue();
    }

    [Fact]
    public void Should_Not_Map_Exception_When_Caller_Cancelled_Request()
    {
        ApiErrorMapper.FromException(new OperationCanceledException()).ShouldBeNull();
    }

    private static HttpResponseMessage Respond(HttpStatusCode statusCode, string content, string mediaType) =>
        new(statusCode) { Content = new StringContent(content, Encoding.UTF8, mediaType) };

    private static async Task<ApiError> StartWorkAsync(HttpResponseMessage response)
    {
        using var handler = new StubHttpMessageHandler(_ => response);
        return await StartWorkAsync(handler);
    }

    private static async Task<ApiError> StartWorkAsync(StubHttpMessageHandler handler)
    {
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        var api = RestService.For<IFixFlowApi>(httpClient);

        using var apiResponse = await api.StartWorkAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        apiResponse.IsSuccessful.ShouldBeFalse();
        return ApiErrorMapper.FromResponse(apiResponse);
    }
}
