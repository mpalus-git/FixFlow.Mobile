namespace FixFlow.Mobile.Core.Features.Sync;

public sealed record PendingOperationView(Guid OperationId, string Title, string Details, string StatusText, bool IsHeld)
{
    public bool HasDetails => Details.Length > 0;
}
