using FixFlow.Mobile.Core.Features.ServiceEntries;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed record WorkOrderDetailsSources(
    WorkOrderRepository WorkOrders,
    ClientRepository Clients,
    DeviceRepository Devices,
    ServiceEntryRepository ServiceEntries,
    DeviceHistoryRepository DeviceHistory);
