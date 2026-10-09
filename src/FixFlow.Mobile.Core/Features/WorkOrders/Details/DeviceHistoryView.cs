namespace FixFlow.Mobile.Core.Features.WorkOrders.Details;

public sealed record DeviceHistoryView(Guid WorkOrderId, string Number, string StatusText, BadgeTone StatusTone, string DateText, string? TechnicianName, string EntryCountText);
