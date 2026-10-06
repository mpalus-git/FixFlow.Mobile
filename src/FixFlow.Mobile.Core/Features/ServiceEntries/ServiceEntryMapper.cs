using FixFlow.Mobile.Core.Api;

namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public static class ServiceEntryMapper
{
    public static ServiceEntry FromResponse(ServiceEntryResponse entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new ServiceEntry(
            entry.Id,
            entry.WorkOrderId,
            entry.TechnicianId,
            entry.TechnicianName,
            entry.Note,
            entry.IsCorrection,
            entry.WorkStartedAt,
            entry.WorkFinishedAt,
            entry.Latitude,
            entry.Longitude,
            entry.CreatedAt,
            [.. entry.Parts.Select(part => new ServiceEntryPart(part.PartId, part.PartName, part.CatalogNumber, part.Quantity, part.UnitPrice))])
        {
            PhotoUrls = [.. entry.PhotoUrls],
        };
    }
}
