namespace FixFlow.Mobile.Core.Photos;

public static class PhotoCompression
{
    public const int MaxLongSide = 1600;

    public const int JpegQuality = 75;

    public const int MaxBytes = 1024 * 1024;

    public static bool IsJpeg(ReadOnlySpan<byte> content) => content is [0xFF, 0xD8, 0xFF, ..];
}
