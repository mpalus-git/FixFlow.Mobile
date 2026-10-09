using System.Windows.Input;
using CommunityToolkit.Maui.Core.Views;
using CommunityToolkit.Maui.Views;
using FixFlow.Mobile.Core.Photos;
using Microsoft.Maui.Graphics.Platform;

namespace FixFlow.Mobile.Features.WorkOrders;

public sealed class SignaturePad : DrawingView, ISignatureSource
{
    public SignaturePad()
    {
        ClearCommand = new Command(Clear);
    }

    public ICommand ClearCommand { get; }

    public bool IsEmpty => Lines.Count == 0;

    public async Task<byte[]> ExportJpegAsync(CancellationToken cancellationToken)
    {
        await using var png = await GetImageStream(
            ImageLineOptions.FullCanvas([.. Lines], new Size(SignatureImage.Width, SignatureImage.Height), new SolidPaint(Colors.White), new Size(Width, Height)),
            cancellationToken);
        using var image = PlatformImage.FromStream(png);
        using var jpeg = new MemoryStream();
        await image.SaveAsync(jpeg, ImageFormat.Jpeg, SignatureImage.JpegQuality);
        return jpeg.ToArray();
    }
}
