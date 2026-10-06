using System.Net.Http.Headers;
using Microsoft.UI.Xaml.Media.Imaging;
using WImageSource = Microsoft.UI.Xaml.Media.ImageSource;

namespace FixFlow.Mobile;

public sealed class UserAgentUriImageSourceService : UriImageSourceService
{
    private static readonly HttpClient Client = CreateClient();

    public override async Task<IImageSourceServiceResult<WImageSource>?> GetImageSourceAsync(
        IImageSource imageSource, float scale = 1, CancellationToken cancellationToken = default)
    {
        if (imageSource is not IUriImageSource { IsEmpty: false, Uri: { } uri })
        {
            return null;
        }

        using var response = await Client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var content = new MemoryStream(await response.Content.ReadAsByteArrayAsync(cancellationToken));
        var image = new BitmapImage();
        await image.SetSourceAsync(content.AsRandomAccessStream());
        return new ImageSourceServiceResult(image);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FixFlow.Mobile", AppInfo.Current.VersionString));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+https://github.com/mpalus-git/FixFlow.Mobile)"));
        return client;
    }
}
