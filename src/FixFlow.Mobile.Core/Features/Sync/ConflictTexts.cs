using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.Sync;

public static class ConflictTexts
{
    public static string Reason(SyncConflict conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        if (conflict.ErrorCode is { } code && Text($"ConflictReason{code.Replace(".", string.Empty, StringComparison.Ordinal)}") is { } translated)
        {
            return translated;
        }

        if (!string.IsNullOrWhiteSpace(conflict.Detail))
        {
            return conflict.Detail;
        }

        return conflict.StatusCode is { } status
            ? WorkOrderTexts.Format(AppResources.ConflictReasonRejected, (int)status)
            : AppResources.ConflictReasonUnknown;
    }

    public static string Kind(OutboxOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return operation.Kind switch
        {
            OutboxOperationKind.StartWork => AppResources.ChangeKindStartWork,
            OutboxOperationKind.AddServiceEntry when OutboxPayload.ToServiceEntry(operation).IsCorrection => AppResources.ChangeKindCorrection,
            OutboxOperationKind.AddServiceEntry => AppResources.ChangeKindServiceEntry,
            OutboxOperationKind.CompleteWorkOrder => AppResources.ChangeKindCompleteWorkOrder,
            _ => AppResources.ChangeKindUploadPhoto,
        };
    }

    private static string? Text(string key) => AppResources.ResourceManager.GetString(key, AppResources.Culture);
}
