namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public sealed record PendingServiceEntryPart(Guid PartId, string PartName, string CatalogNumber, int Quantity, decimal UnitPrice);
