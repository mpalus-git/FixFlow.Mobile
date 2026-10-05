namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public sealed record ServiceEntryView(
    Guid Id,
    string TechnicianName,
    string CreatedText,
    string Note,
    bool IsCorrection,
    string? WorkTimeText,
    IReadOnlyList<ServiceEntryPartView> Parts,
    double? Latitude,
    double? Longitude)
{
    public bool HasLocation => Latitude is not null && Longitude is not null;

    public bool HasParts => Parts.Count > 0;
}
