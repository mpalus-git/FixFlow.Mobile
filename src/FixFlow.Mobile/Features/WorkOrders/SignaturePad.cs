using System.Windows.Input;
using FixFlow.Mobile.Core.Photos;

namespace FixFlow.Mobile.Features.WorkOrders;

public sealed partial class SignaturePad : GraphicsView, IDrawable, ISignatureSource
{
    private const float StrokeSize = 4;

    private readonly List<List<PointF>> _strokes = [];

    public SignaturePad()
    {
        Drawable = this;
        ClearCommand = new Command(Clear);
        StartInteraction += OnStartInteraction;
        DragInteraction += OnDragInteraction;
    }

    public ICommand ClearCommand { get; }

    public bool IsEmpty => _strokes.Count == 0;

    public void Draw(ICanvas canvas, RectF dirtyRect) => DrawSignature(canvas, 1);

    public Task<byte[]> ExportJpegAsync(CancellationToken cancellationToken)
    {
        var scale = (float)Math.Min(SignatureImage.Width / Width, SignatureImage.Height / Height);
        return RenderJpegAsync((int)Math.Round(Width * scale), (int)Math.Round(Height * scale), scale);
    }

    private partial Task<byte[]> RenderJpegAsync(int width, int height, float scale);

    private void OnStartInteraction(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length > 0)
        {
            _strokes.Add([e.Touches[0]]);
            Invalidate();
        }
    }

    private void OnDragInteraction(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length > 0 && _strokes.Count > 0)
        {
            _strokes[^1].Add(e.Touches[0]);
            Invalidate();
        }
    }

    private void Clear()
    {
        _strokes.Clear();
        Invalidate();
    }

    private void DrawSignature(ICanvas canvas, float scale)
    {
        canvas.FillColor = Colors.White;
        canvas.FillRectangle(0, 0, (float)(Width * scale), (float)(Height * scale));
        canvas.StrokeColor = Colors.Black;
        canvas.FillColor = Colors.Black;
        canvas.StrokeSize = StrokeSize * scale;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        foreach (var stroke in _strokes)
        {
            if (stroke.Count == 1)
            {
                canvas.FillCircle(stroke[0].X * scale, stroke[0].Y * scale, StrokeSize * scale / 2);
                continue;
            }

            var path = new PathF(stroke[0].X * scale, stroke[0].Y * scale);
            foreach (var point in stroke.Skip(1))
            {
                path.LineTo(point.X * scale, point.Y * scale);
            }

            canvas.DrawPath(path);
        }
    }
}
