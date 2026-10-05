using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed class WorkOrderDetailsPresenter(DisplayFormatter formatter)
{
    public WorkOrderDetailsView Present(
        WorkOrder workOrder,
        Client? client,
        Device? device,
        IReadOnlyList<ServiceEntry> entries,
        IReadOnlyList<DeviceHistoryItem> history)
    {
        ArgumentNullException.ThrowIfNull(workOrder);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(history);

        var usedParts = entries.SelectMany(entry => entry.Parts.Select(part => entry.IsCorrection ? -part.Amount : part.Amount)).ToList();
        return new WorkOrderDetailsView(
            workOrder.Number,
            WorkOrderTexts.Status(workOrder.Status),
            WorkOrderTexts.Priority(workOrder.Priority),
            workOrder.Priority is WorkOrderPriority.High or WorkOrderPriority.Critical && WorkOrderTexts.IsActive(workOrder.Status),
            WorkOrderTexts.Format(AppResources.WorkOrderDueAt, formatter.FormatDateTime(workOrder.DueDate)),
            workOrder.Description,
            client is null ? null : ToClientView(client),
            device is null ? null : ToDeviceView(device),
            [.. entries.Select(ToEntryView)],
            usedParts.Count == 0 ? null : WorkOrderTexts.Format(AppResources.DetailsPartsTotal, formatter.FormatMoney(usedParts.Sum())),
            [.. history.Where(item => item.WorkOrderId != workOrder.Id).Select(ToHistoryView)],
            workOrder.Status is WorkOrderStatus.Completed or WorkOrderStatus.Invoiced);
    }

    private static ClientView ToClientView(Client client) => new(
        client.Name,
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

    private ServiceEntryView ToEntryView(ServiceEntry entry) => new(
        entry.Id,
        entry.TechnicianName,
        formatter.FormatDateTime(entry.CreatedAt),
        entry.Note,
        entry.IsCorrection,
        entry is { WorkStartedAt: { } started, WorkFinishedAt: { } finished } ? FormatWorkTime(started, finished) : null,
        [.. entry.Parts.Select(part => ToPartView(part, entry.IsCorrection))],
        entry.Latitude,
        entry.Longitude);

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

    private DeviceHistoryView ToHistoryView(DeviceHistoryItem item) => new(
        item.WorkOrderId,
        item.Number,
        WorkOrderTexts.Status(item.Status),
        formatter.FormatDateTime(item.CompletedAt ?? item.CreatedAt),
        item.TechnicianName,
        WorkOrderTexts.Format(AppResources.DetailsEntryCount, item.ServiceEntryCount));

    private string FormatWorkTime(DateTimeOffset started, DateTimeOffset finished)
    {
        var duration = finished - started;
        return WorkOrderTexts.Format(
            AppResources.DetailsWorkTime,
            formatter.FormatDateTime(started),
            formatter.FormatTime(finished),
            WorkOrderTexts.Format(AppResources.DetailsDuration, (int)duration.TotalHours, duration.Minutes));
    }
}
