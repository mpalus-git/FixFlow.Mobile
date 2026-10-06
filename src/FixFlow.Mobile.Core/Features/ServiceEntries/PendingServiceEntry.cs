namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public sealed record PendingServiceEntry(
    Guid Id,
    string Note,
    bool IsCorrection,
    DateTimeOffset? WorkStartedAt,
    DateTimeOffset? WorkFinishedAt,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<PendingServiceEntryPart> Parts)
{
    public IReadOnlyList<PendingPhoto> Photos { get; init; } = [];
}
