using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed class WorkOrderDetailsPresenter(DisplayFormatter formatter, WorkOrderSectionClassifier classifier, SessionStore session, ApiAddress apiAddress)
{
    public WorkOrderDetailsView Present(
        WorkOrder workOrder,
        Device? device,
        IReadOnlyList<ServiceEntry> entries,
        IReadOnlyList<DeviceHistoryItem> history,
        IReadOnlyList<WorkOrderEvent> events,
        IReadOnlyList<OutboxOperation> pendingOperations,
        Func<Guid, string?>? localPhotoPath = null)
    {
        ArgumentNullException.ThrowIfNull(workOrder);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(pendingOperations);

        var pendingEntries = pendingOperations
            .Where(operation => operation.Kind == OutboxOperationKind.AddServiceEntry)
            .Select(operation => (Entry: ToServiceEntry(workOrder.Id, operation), OutboxPayload.ToServiceEntry(operation).Photos))
            .ToList();
        var allEntries = entries.Concat(pendingEntries.Select(pending => pending.Entry)).ToList();
        var usedParts = allEntries.SelectMany(entry => entry.Parts.Select(part => entry.IsCorrection ? -part.Amount : part.Amount)).ToList();
        return new WorkOrderDetailsView(
            workOrder.Number,
            WorkOrderTexts.Status(workOrder.Status),
            WorkOrderBadges.ForStatus(workOrder.Status),
            WorkOrderTexts.Priority(workOrder.Priority),
            WorkOrderBadges.ForPriority(workOrder.Priority),
            workOrder.Priority is WorkOrderPriority.High or WorkOrderPriority.Critical && WorkOrderTexts.IsActive(workOrder.Status),
            classifier.Classify(workOrder) == WorkOrderSection.Overdue,
            WorkOrderTexts.Format(AppResources.WorkOrderDueAt, formatter.FormatDateTime(workOrder.DueDate)),
            workOrder.Description,
            workOrder.ClientContact.IsEmpty ? null : ToClientView(workOrder.ClientName, workOrder.ClientContact),
            device is null ? null : ToDeviceView(device),
            [
                .. entries.Select(entry => ToEntryView(entry, isPending: false, [.. entry.PhotoUrls.Select(url => new EntryPhotoView(url, false))])),
                .. pendingEntries.Select(pending => ToEntryView(pending.Entry, isPending: true, [.. pending.Photos.Select(photo => ToPhotoView(photo, localPhotoPath))])),
            ],
            usedParts.Count == 0 ? null : WorkOrderTexts.Format(AppResources.DetailsPartsTotal, formatter.FormatMoney(usedParts.Sum())),
            [.. history.Where(item => item.WorkOrderId != workOrder.Id).Select(ToHistoryView)],
            [.. events.Select((item, index) => ToEventView(item, index == 0 ? item.DueDate : events[index - 1].DueDate)), .. PendingEventViews(pendingOperations)],
            workOrder.ClientSignaturePhotoId is { } signatureId ? ToSignatureView(signatureId, localPhotoPath) : null,
            workOrder.Status is WorkOrderStatus.Completed or WorkOrderStatus.Invoiced
                && !pendingOperations.Any(operation => operation.Kind == OutboxOperationKind.CompleteWorkOrder));
    }

    public WorkTimerView PresentWorkTimer(WorkTimer timer, DateTimeOffset now, bool isLocating)
    {
        ArgumentNullException.ThrowIfNull(timer);

        var elapsed = now > timer.StartedAt ? now - timer.StartedAt : TimeSpan.Zero;
        return new WorkTimerView(
            WorkOrderTexts.Format(AppResources.WorkTimerStartedAt, formatter.FormatDateTime(timer.StartedAt)),
            WorkOrderTexts.Format(AppResources.WorkTimerElapsed, WorkOrderTexts.Format(AppResources.DetailsDuration, (int)elapsed.TotalHours, elapsed.Minutes)),
            isLocating ? AppResources.WorkLocationLocating : timer.LocationStatus switch
            {
                WorkLocationStatus.Captured => AppResources.WorkLocationCaptured,
                WorkLocationStatus.Denied => AppResources.WorkLocationDenied,
                WorkLocationStatus.NotRequired => null,
                _ => AppResources.WorkLocationUnavailable,
            },
            isLocating,
            !isLocating && timer.LocationStatus is not (WorkLocationStatus.Captured or WorkLocationStatus.NotRequired),
            !isLocating && timer.LocationStatus == WorkLocationStatus.Denied);
    }

    private static ClientView ToClientView(string name, ClientContact client) => new(
        name,
        WorkOrderTexts.Format(AppResources.DetailsContactPerson, client.ContactPerson),
        client.Phone,
        $"{client.Street} {client.BuildingNumber}, {client.PostalCode} {client.City}",
        $"{client.Street} {client.BuildingNumber}",
        client.PostalCode,
        client.City);

    private DeviceView ToDeviceView(Device device) => new(
        $"{device.Manufacturer} {device.Model}",
        WorkOrderTexts.Format(AppResources.DetailsSerialNumber, device.SerialNumber),
        WorkOrderTexts.Format(AppResources.DetailsInstalledOn, formatter.FormatDate(device.InstallationDate)));

    private ServiceEntry ToServiceEntry(Guid workOrderId, OutboxOperation operation)
    {
        var pending = OutboxPayload.ToServiceEntry(operation);
        var author = session.CurrentUser;
        return new ServiceEntry(
            pending.Id,
            workOrderId,
            author?.Id ?? Guid.Empty,
            author?.FullName ?? string.Empty,
            pending.Note,
            pending.IsCorrection,
            pending.WorkStartedAt,
            pending.WorkFinishedAt,
            pending.Latitude,
            pending.Longitude,
            operation.CreatedAt,
            [.. pending.Parts.Select(part => new ServiceEntryPart(part.PartId, part.PartName, part.CatalogNumber, part.Quantity, part.UnitPrice))]);
    }

    private EntryPhotoView ToSignatureView(Guid signatureId, Func<Guid, string?>? localPhotoPath) =>
        localPhotoPath?.Invoke(signatureId) is { } path ? new EntryPhotoView(path, true) : new EntryPhotoView(apiAddress.PhotoUrl(signatureId), false);

    private static EntryPhotoView ToPhotoView(PendingPhoto photo, Func<Guid, string?>? localPhotoPath) =>
        localPhotoPath?.Invoke(photo.Id) is { } path ? new EntryPhotoView(path, true) : new EntryPhotoView(photo.Url, false);

    private ServiceEntryView ToEntryView(ServiceEntry entry, bool isPending, IReadOnlyList<EntryPhotoView> photos) => new(
        entry.Id,
        entry.TechnicianName,
        formatter.FormatDateTime(entry.CreatedAt),
        entry.Note,
        entry.IsCorrection,
        entry is { WorkStartedAt: { } started, WorkFinishedAt: { } finished } ? FormatWorkTime(started, finished) : null,
        [.. entry.Parts.Select(part => ToPartView(part, entry.IsCorrection))],
        entry.Latitude,
        entry.Longitude,
        isPending,
        photos);

    private ServiceEntryPartView ToPartView(ServiceEntryPart part, bool isCorrection)
    {
        var sign = isCorrection ? -1 : 1;
        return new ServiceEntryPartView(
            part.PartName,
            part.CatalogNumber,
            WorkOrderTexts.Format(AppResources.DetailsQuantity, sign * part.Quantity),
            formatter.FormatMoney(part.UnitPrice),
            formatter.FormatMoney(sign * part.Amount));
    }

    private WorkOrderEventView ToEventView(WorkOrderEvent item, DateTimeOffset previousDueDate) => new(
        item.Type switch
        {
            WorkOrderEventType.Created => AppResources.EventCreated,
            WorkOrderEventType.Updated when item.DueDate != previousDueDate =>
                WorkOrderTexts.Format(AppResources.EventDueDateChanged, formatter.FormatDateTime(item.DueDate)),
            WorkOrderEventType.Updated => AppResources.EventUpdated,
            WorkOrderEventType.Assigned => WorkOrderTexts.Format(AppResources.EventAssigned, item.TechnicianName ?? string.Empty),
            WorkOrderEventType.Reassigned => WorkOrderTexts.Format(AppResources.EventReassigned, item.TechnicianName ?? string.Empty),
            WorkOrderEventType.Unassigned => AppResources.EventUnassigned,
            WorkOrderEventType.Started => AppResources.EventStarted,
            WorkOrderEventType.Completed => AppResources.EventCompleted,
            _ => AppResources.EventInvoiced,
        },
        formatter.FormatDateTime(item.OccurredAt),
        item.ActorName ?? AppResources.EventSystemActor,
        IsPending: false);

    private IEnumerable<WorkOrderEventView> PendingEventViews(IReadOnlyList<OutboxOperation> pendingOperations)
    {
        var author = session.CurrentUser?.FullName ?? string.Empty;
        return pendingOperations
            .Where(operation => operation.Kind is OutboxOperationKind.StartWork or OutboxOperationKind.CompleteWorkOrder)
            .Select(operation => operation.Kind == OutboxOperationKind.StartWork
                ? new WorkOrderEventView(AppResources.EventStarted, formatter.FormatDateTime(OutboxPayload.StartedAt(operation)), author, IsPending: true)
                : new WorkOrderEventView(AppResources.EventCompleted, formatter.FormatDateTime(OutboxPayload.CompletedAt(operation)), author, IsPending: true));
    }

    private DeviceHistoryView ToHistoryView(DeviceHistoryItem item) => new(
        item.WorkOrderId,
        item.Number,
        WorkOrderTexts.Status(item.Status),
        WorkOrderBadges.ForStatus(item.Status),
        formatter.FormatDateTime(item.CompletedAt ?? item.CreatedAt),
        item.TechnicianName,
        WorkOrderTexts.Format(AppResources.DetailsEntryCount, item.ServiceEntryCount));

    private string FormatWorkTime(DateTimeOffset started, DateTimeOffset finished)
    {
        var duration = finished - started;
        return WorkOrderTexts.Format(
            AppResources.DetailsWorkTime,
            formatter.FormatDateTime(started),
            formatter.IsSameBusinessDay(started, finished) ? formatter.FormatTime(finished) : formatter.FormatDateTime(finished),
            WorkOrderTexts.Format(AppResources.DetailsDuration, (int)duration.TotalHours, duration.Minutes));
    }
}
