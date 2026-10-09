using FixFlow.Mobile.Core.Features.Sync.Outbox;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.Sync.Conflicts;

public sealed class LocalChangeDescriber(DisplayFormatter formatter)
{
    public string Describe(OutboxOperation operation, string? workOrderNumber) =>
        string.Join(Environment.NewLine, DescribeLines(operation, workOrderNumber));

    public IReadOnlyList<string> DescribeLines(OutboxOperation operation, string? workOrderNumber)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var kind = ConflictTexts.Kind(operation);
        List<string> lines = [workOrderNumber is null ? kind : AppText.Format(AppResources.ChangeHeader, workOrderNumber, kind)];
        if (operation.Kind == OutboxOperationKind.StartWork)
        {
            lines.Add(AppText.Format(AppResources.ChangeStartedAt, formatter.FormatDateTime(OutboxPayload.StartedAt(operation))));
        }
        else if (operation.Kind == OutboxOperationKind.CompleteWorkOrder)
        {
            lines.Add(AppText.Format(AppResources.ChangeCompletedAt, formatter.FormatDateTime(OutboxPayload.CompletedAt(operation))));
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
            lines.Add(AppText.Format(AppResources.ChangeWorkTime, formatter.FormatDateTime(startedAt), formatter.FormatDateTime(finishedAt)));
        }

        lines.Add(AppText.Format(entry.IsCorrection ? AppResources.ChangeCorrectionReason : AppResources.ChangeNote, entry.Note));
        if (entry.Parts.Count > 0)
        {
            lines.Add(entry.IsCorrection ? AppResources.ChangeReturnedParts : AppResources.ChangeUsedParts);
            lines.AddRange(entry.Parts.Select(part => AppText.Format(AppResources.ChangePartLine, part.PartName, part.CatalogNumber, part.Quantity)));
        }

        if (entry.Photos.Count > 0)
        {
            lines.Add(AppText.Format(AppResources.ChangePhotoCount, entry.Photos.Count));
        }
    }
}
