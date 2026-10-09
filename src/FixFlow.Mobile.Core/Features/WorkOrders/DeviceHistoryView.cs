namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed record DeviceHistoryView(Guid WorkOrderId, string Number, string StatusText, BadgeTone StatusTone, string DateText, string? TechnicianName, string EntryCountText);
