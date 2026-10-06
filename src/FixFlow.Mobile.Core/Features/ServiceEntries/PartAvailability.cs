using FixFlow.Mobile.Core.Features.Parts;

namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public static class PartAvailability
{
    public static int Available(Part part, IEnumerable<PendingServiceEntry> pendingEntries)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(pendingEntries);

        return part.StockQuantity - pendingEntries
            .SelectMany(entry => entry.Parts.Where(used => used.PartId == part.Id).Select(used => entry.IsCorrection ? -used.Quantity : used.Quantity))
            .Sum();
    }
}
