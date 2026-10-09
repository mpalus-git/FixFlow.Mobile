using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync.Outbox;
using FixFlow.Mobile.Core.Features.WorkOrders.WorkTime;

namespace FixFlow.Mobile.Core.Features.WorkOrders.Details;

public sealed record WorkOrderDetailsSources(
    WorkOrderRepository WorkOrders,
    DeviceRepository Devices,
    ServiceEntryRepository ServiceEntries,
    DeviceHistoryRepository DeviceHistory,
    OutboxRepository Outbox,
    WorkTimerRepository WorkTimers,
    WorkOrderEventRepository WorkOrderEvents);
