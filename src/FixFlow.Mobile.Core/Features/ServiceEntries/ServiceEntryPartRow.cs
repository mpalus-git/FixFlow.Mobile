using System.Globalization;
using SQLite;

namespace FixFlow.Mobile.Core.Features.ServiceEntries;

[Table("service_entry_parts")]
internal sealed class ServiceEntryPartRow
{
    [Column("service_entry_id")]
    public Guid ServiceEntryId { get; set; }

    [Column("position")]
    public int Position { get; set; }

    [Column("part_id")]
    public Guid PartId { get; set; }

    [Column("part_name")]
    public string PartName { get; set; } = string.Empty;

    [Column("catalog_number")]
    public string CatalogNumber { get; set; } = string.Empty;

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("unit_price")]
    public string UnitPrice { get; set; } = string.Empty;

    public static ServiceEntryPartRow From(Guid serviceEntryId, int position, ServiceEntryPart part) => new()
    {
        ServiceEntryId = serviceEntryId,
        Position = position,
        PartId = part.PartId,
        PartName = part.PartName,
        CatalogNumber = part.CatalogNumber,
        Quantity = part.Quantity,
        UnitPrice = part.UnitPrice.ToString(CultureInfo.InvariantCulture),
    };

    public ServiceEntryPart ToPart() =>
        new(PartId, PartName, CatalogNumber, Quantity, decimal.Parse(UnitPrice, CultureInfo.InvariantCulture));
}
