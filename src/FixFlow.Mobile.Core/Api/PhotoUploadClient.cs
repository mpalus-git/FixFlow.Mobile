using System.Net.Http.Headers;
using FixFlow.Mobile.Core.Api.Errors;

namespace FixFlow.Mobile.Core.Api;

public sealed class PhotoUploadClient(HttpClient httpClient)
{
    public const string JpegMediaType = "image/jpeg";

    public async Task<ApiError?> UploadAsync(Guid photoId, byte[] jpeg, CancellationToken cancellationToken)
    {
        using var content = new ByteArrayContent(jpeg);
        content.Headers.ContentType = new MediaTypeHeaderValue(JpegMediaType);
        try
        {
            using var response = await httpClient.PutAsync(new Uri($"api/v1/photos/{photoId}", UriKind.Relative), content, cancellationToken);
            return response.IsSuccessStatusCode
                ? null
                : ApiErrorMapper.FromResponse(response, await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception exception) when (ApiErrorMapper.FromException(exception) is { } error)
        {
            return error;
        }
    }
}
