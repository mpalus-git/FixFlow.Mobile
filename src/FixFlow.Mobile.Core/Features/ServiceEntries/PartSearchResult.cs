using FixFlow.Mobile.Core.Features.Parts;

namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public sealed record PartSearchResult(Part Part, int Available, string DetailsText, string AvailabilityText, bool CanAdd);
