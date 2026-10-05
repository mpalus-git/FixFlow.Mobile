using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed class WorkOrderDetailsPresenter(DisplayFormatter formatter)
{
    public WorkOrderDetailsView Present(
        WorkOrder workOrder,
        Client? client,
        Device? device,
        IReadOnlyList<DeviceHistoryItem> history)
    {
        ArgumentNullException.ThrowIfNull(workOrder);
        ArgumentNullException.ThrowIfNull(history);

        return new WorkOrderDetailsView(
            workOrder.Number,
            WorkOrderTexts.Status(workOrder.Status),
            WorkOrderTexts.Priority(workOrder.Priority),
            workOrder.Priority is WorkOrderPriority.High or WorkOrderPriority.Critical && WorkOrderTexts.IsActive(workOrder.Status),
            WorkOrderTexts.Format(AppResources.WorkOrderDueAt, formatter.FormatDateTime(workOrder.DueDate)),
            workOrder.Description,
            client is null ? null : ToClientView(client),
            device is null ? null : ToDeviceView(device),
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

    private DeviceHistoryView ToHistoryView(DeviceHistoryItem item) => new(
        item.WorkOrderId,
        item.Number,
        WorkOrderTexts.Status(item.Status),
        formatter.FormatDateTime(item.CompletedAt ?? item.CreatedAt),
        item.TechnicianName,
        WorkOrderTexts.Format(AppResources.DetailsEntryCount, item.ServiceEntryCount));
}
