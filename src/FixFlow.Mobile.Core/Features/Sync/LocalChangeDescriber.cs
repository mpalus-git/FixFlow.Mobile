using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.Sync;

public sealed class LocalChangeDescriber(DisplayFormatter formatter)
{
    public string Describe(OutboxOperation operation, string? workOrderNumber) =>
        string.Join(Environment.NewLine, DescribeLines(operation, workOrderNumber));

    public IReadOnlyList<string> DescribeLines(OutboxOperation operation, string? workOrderNumber)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var kind = ConflictTexts.Kind(operation);
        List<string> lines = [workOrderNumber is null ? kind : WorkOrderTexts.Format(AppResources.ChangeHeader, workOrderNumber, kind)];
        if (operation.Kind == OutboxOperationKind.StartWork)
        {
            lines.Add(WorkOrderTexts.Format(AppResources.ChangeStartedAt, formatter.FormatDateTime(OutboxPayload.StartedAt(operation))));
        }
        else if (operation.Kind == OutboxOperationKind.CompleteWorkOrder)
        {
            lines.Add(WorkOrderTexts.Format(AppResources.ChangeCompletedAt, formatter.FormatDateTime(OutboxPayload.CompletedAt(operation))));
            if (OutboxPayload.ClientSignaturePhotoId(operation) is not null)
            {
                lines.Add(AppResources.ChangeClientSignature);
            }
        }
        else if (operation.Kind == OutboxOperationKind.AddServiceEntry)
        {
            AddServiceEntryLines(lines, operation);
        }

        return lines;
    }

    private void AddServiceEntryLines(List<string> lines, OutboxOperation operation)
    {
        var entry = OutboxPayload.ToServiceEntry(operation);
        if (entry is { WorkStartedAt: { } startedAt, WorkFinishedAt: { } finishedAt })
        {
            lines.Add(WorkOrderTexts.Format(AppResources.ChangeWorkTime, formatter.FormatDateTime(startedAt), formatter.FormatDateTime(finishedAt)));
        }

        lines.Add(WorkOrderTexts.Format(entry.IsCorrection ? AppResources.ChangeCorrectionReason : AppResources.ChangeNote, entry.Note));
        if (entry.Parts.Count > 0)
        {
            lines.Add(entry.IsCorrection ? AppResources.ChangeReturnedParts : AppResources.ChangeUsedParts);
            lines.AddRange(entry.Parts.Select(part => WorkOrderTexts.Format(AppResources.ChangePartLine, part.PartName, part.CatalogNumber, part.Quantity)));
        }

        if (entry.Photos.Count > 0)
        {
            lines.Add(WorkOrderTexts.Format(AppResources.ChangePhotoCount, entry.Photos.Count));
        }
    }
}
