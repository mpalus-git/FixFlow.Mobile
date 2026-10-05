namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public sealed record ServiceEntryPart(Guid PartId, string PartName, string CatalogNumber, int Quantity, decimal UnitPrice)
{
    public decimal Amount => Quantity * UnitPrice;
}
