using System.Globalization;
using SQLite;

namespace FixFlow.Mobile.Core.Features.Parts;

[Table("parts")]
internal sealed class PartRow
{
    [PrimaryKey]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("catalog_number")]
    public string CatalogNumber { get; set; } = string.Empty;

    [Column("stock_quantity")]
    public int StockQuantity { get; set; }

    [Column("unit_price")]
    public string UnitPrice { get; set; } = string.Empty;

    [Column("archived_at")]
    public DateTimeOffset? ArchivedAt { get; set; }

    [Column("search_text")]
    public string SearchText { get; set; } = string.Empty;

    public static string NormalizeForSearch(string text) => text.Trim().ToLower(CultureInfo.InvariantCulture);

    public static PartRow From(Part part) => new()
    {
        Id = part.Id,
        Name = part.Name,
        CatalogNumber = part.CatalogNumber,
        StockQuantity = part.StockQuantity,
        UnitPrice = part.UnitPrice.ToString(CultureInfo.InvariantCulture),
        ArchivedAt = part.ArchivedAt,
        SearchText = NormalizeForSearch($"{part.Name} {part.CatalogNumber}"),
    };

    public Part ToPart() => new(
        Id,
        Name,
        CatalogNumber,
        StockQuantity,
        decimal.Parse(UnitPrice, CultureInfo.InvariantCulture),
        ArchivedAt);
}
