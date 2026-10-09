using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed record WorkOrderDetailsSources(
    WorkOrderRepository WorkOrders,
    DeviceRepository Devices,
    ServiceEntryRepository ServiceEntries,
    DeviceHistoryRepository DeviceHistory,
    OutboxRepository Outbox,
    WorkTimerRepository WorkTimers,
    WorkOrderEventRepository WorkOrderEvents);
