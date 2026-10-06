namespace FixFlow.Mobile.Core.Api;

public sealed class ApiAddress(Uri baseAddress)
{
    public string PhotoUrl(Guid photoId) => new Uri(baseAddress, $"api/v1/photos/{photoId}").AbsoluteUri;
}
