namespace FixFlow.Mobile.Core.Photos;

public interface ISignatureSource
{
    bool IsEmpty { get; }

    Task<byte[]> ExportJpegAsync(CancellationToken cancellationToken);
}
