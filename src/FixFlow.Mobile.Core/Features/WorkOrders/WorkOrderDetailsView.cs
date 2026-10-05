namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed record WorkOrderDetailsView(
    string Number,
    string StatusText,
    string PriorityText,
    bool IsUrgent,
    string DueText,
    string Description,
    ClientView? Client,
    DeviceView? Device,
    IReadOnlyList<DeviceHistoryView> History,
    bool CanDownloadProtocol);
