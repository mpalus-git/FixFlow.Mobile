using System.Net;
using System.Text;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Api.Errors;

namespace FixFlow.Mobile.UnitTests.Api;

public sealed class PhotoUploadClientTests
{
    private static readonly Guid PhotoId = Guid.NewGuid();
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    [Fact]
    public async Task Should_Put_Raw_Jpeg_Under_Client_Id_When_Photo_Is_Uploaded()
    {
        byte[]? sentBody = null;
        string? sentType = null;
        using var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            sentBody = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            sentType = request.Content.Headers.ContentType?.MediaType;
            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        var error = await CreateClient(handler).UploadAsync(PhotoId, Jpeg, TestContext.Current.CancellationToken);

        error.ShouldBeNull();
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Put);
        request.RequestUri!.AbsolutePath.ShouldBe($"/api/v1/photos/{PhotoId}");
        sentType.ShouldBe("image/jpeg");
        sentBody.ShouldBe(Jpeg);
    }

    [Fact]
    public async Task Should_Report_Business_Error_With_Code_When_Photo_Is_Too_Large()
    {
        using var handler = new StubHttpMessageHandler(_ => Problem(
            HttpStatusCode.RequestEntityTooLarge, """{"status":413,"detail":"Photo is too large.","errorCode":"Photo.ContentTooLarge"}"""));

        var error = await CreateClient(handler).UploadAsync(PhotoId, Jpeg, TestContext.Current.CancellationToken);

        error.ShouldNotBeNull().Kind.ShouldBe(ApiErrorKind.Business);
        error.ErrorCode.ShouldBe("Photo.ContentTooLarge");
    }

    [Fact]
    public async Task Should_Report_Transient_Error_When_Network_Fails()
    {
        using var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("offline"));

        var error = await CreateClient(handler).UploadAsync(PhotoId, Jpeg, TestContext.Current.CancellationToken);

        error.ShouldNotBeNull().ShouldRetry.ShouldBeTrue();
    }

    [Fact]
    public void Should_Compose_Absolute_Photo_Address_When_Entry_Is_Created_Offline()
    {
        new ApiAddress(new Uri("http://10.0.2.2:8080")).PhotoUrl(PhotoId).ShouldBe($"http://10.0.2.2:8080/api/v1/photos/{PhotoId}");
    }

    private static PhotoUploadClient CreateClient(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") });

    private static HttpResponseMessage Problem(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/problem+json") };
}
