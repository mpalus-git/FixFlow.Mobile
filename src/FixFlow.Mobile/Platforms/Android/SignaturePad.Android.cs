using FixFlow.Mobile.Core.Photos;
using Microsoft.Maui.Graphics.Platform;

namespace FixFlow.Mobile.Features.WorkOrders;

public sealed partial class SignaturePad
{
    private partial async Task<byte[]> RenderJpegAsync(int width, int height, float scale)
    {
        using var context = new PlatformBitmapExportService().CreateContext(width, height);
        DrawSignature(context.Canvas, scale);
        using var jpeg = new MemoryStream();
        await context.Image.SaveAsync(jpeg, ImageFormat.Jpeg, SignatureImage.JpegQuality);
        return jpeg.ToArray();
    }
}
