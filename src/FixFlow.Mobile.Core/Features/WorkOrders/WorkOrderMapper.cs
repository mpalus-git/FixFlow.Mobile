using FixFlow.Mobile.Core.Api;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public static class WorkOrderMapper
{
    public static WorkOrder FromResponse(WorkOrderResponse item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new WorkOrder(
            item.Id,
            item.Number,
            item.DeviceId,
            item.DeviceSerialNumber,
            item.DeviceModel,
            item.ClientId,
            item.ClientName,
            item.Description,
            item.Priority,
            item.Status,
            item.DueDate,
            item.IsOverdue,
            item.CreatedAt,
            item.StartedAt,
            item.CompletedAt,
            item.InvoicedAt)
        {
            ClientContact = ToClientContact(item.ClientAddress, item.ClientContactPerson, item.ClientPhone),
        };
    }

    public static Client ToClient(ClientResponse client)
    {
        ArgumentNullException.ThrowIfNull(client);

        return new Client(
            client.Id,
            client.Name,
            client.Address.Street,
            client.Address.BuildingNumber,
            client.Address.PostalCode,
            client.Address.City,
            client.ContactPerson,
            client.Phone,
            client.Email,
            client.ArchivedAt);
    }

    public static Device ToDevice(DeviceResponse device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return new Device(device.Id, device.ClientId, device.SerialNumber, device.Model, device.Manufacturer, device.InstallationDate, device.ArchivedAt);
    }

    public static DeviceHistoryItem ToHistoryItem(Guid deviceId, DeviceWorkOrderHistoryItemResponse item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new DeviceHistoryItem(
            item.Id,
            deviceId,
            item.Number,
            item.Status,
            item.Priority,
            item.Description,
            item.TechnicianName,
            item.CreatedAt,
            item.StartedAt,
            item.CompletedAt,
            item.ServiceEntryCount);
    }

    public static WorkOrder FromListItem(WorkOrderListItemResponse item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new WorkOrder(
            item.Id,
            item.Number,
            item.DeviceId,
            item.DeviceSerialNumber,
            item.DeviceModel,
            item.ClientId,
            item.ClientName,
            item.Description,
            item.Priority,
            item.Status,
            item.DueDate,
            item.IsOverdue,
            item.CreatedAt,
            item.StartedAt,
            item.CompletedAt,
            item.InvoicedAt)
        {
            ClientContact = ToClientContact(item.ClientAddress, item.ClientContactPerson, item.ClientPhone),
        };
    }

    private static ClientContact ToClientContact(ClientAddress address, string contactPerson, string phone) =>
        new(address.Street, address.BuildingNumber, address.PostalCode, address.City, contactPerson, phone);
}
