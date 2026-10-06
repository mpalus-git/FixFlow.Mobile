namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public sealed record NetPartUsage(Guid PartId, string PartName, string CatalogNumber, int NetQuantity, decimal LastUnitPrice)
{
    public static IReadOnlyList<NetPartUsage> Calculate(IEnumerable<ServiceEntry> storedEntries, IEnumerable<PendingServiceEntry> pendingEntries)
    {
        ArgumentNullException.ThrowIfNull(storedEntries);
        ArgumentNullException.ThrowIfNull(pendingEntries);

        var lines = storedEntries
            .SelectMany(entry => entry.Parts.Select(part => (entry.IsCorrection, part.PartId, part.PartName, part.CatalogNumber, part.Quantity, part.UnitPrice)))
            .Concat(pendingEntries.SelectMany(entry =>
                entry.Parts.Select(part => (entry.IsCorrection, part.PartId, part.PartName, part.CatalogNumber, part.Quantity, part.UnitPrice))))
            .ToList();

        return
        [
            .. lines
                .GroupBy(line => line.PartId)
                .Select(group =>
                {
                    var lastUse = group.LastOrDefault(line => !line.IsCorrection);
                    var net = group.Sum(line => line.IsCorrection ? -line.Quantity : line.Quantity);
                    return new NetPartUsage(group.Key, group.First().PartName, group.First().CatalogNumber, net, lastUse.UnitPrice);
                })
                .Where(usage => usage.NetQuantity > 0)
                .OrderBy(usage => usage.PartName, StringComparer.CurrentCulture),
        ];
    }
}
