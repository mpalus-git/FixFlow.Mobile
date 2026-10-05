namespace FixFlow.Mobile.Core.Features.Parts;

public sealed record Part(
    Guid Id,
    string Name,
    string CatalogNumber,
    int StockQuantity,
    decimal UnitPrice,
    DateTimeOffset? ArchivedAt)
{
    public bool IsArchived => ArchivedAt is not null;
}
