using FixFlow.Mobile.Core.Photos;
using Microsoft.Graphics.Canvas;
using Microsoft.Maui.Graphics.Platform;
using Windows.Storage.Streams;

namespace FixFlow.Mobile.Features.WorkOrders;

public sealed partial class SignaturePad
{
    private partial async Task<byte[]> RenderJpegAsync(int width, int height, float scale)
    {
        using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), width, height, 96);
        using (var session = target.CreateDrawingSession())
        using (var canvas = new PlatformCanvas { Session = session, CanvasSize = new(width, height) })
        {
            DrawSignature(canvas, scale);
        }

        using var stream = new InMemoryRandomAccessStream();
        await target.SaveAsync(stream, CanvasBitmapFileFormat.Jpeg, SignatureImage.JpegQuality);
        stream.Seek(0);
        using var jpeg = new MemoryStream();
        await stream.AsStreamForRead().CopyToAsync(jpeg);
        return jpeg.ToArray();
    }
}
