namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public sealed record ServiceEntry(
    Guid Id,
    Guid WorkOrderId,
    Guid TechnicianId,
    string TechnicianName,
    string Note,
    bool IsCorrection,
    DateTimeOffset? WorkStartedAt,
    DateTimeOffset? WorkFinishedAt,
    double? Latitude,
    double? Longitude,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ServiceEntryPart> Parts)
{
    public IReadOnlyList<string> PhotoUrls { get; init; } = [];
}
