namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed record Device(
    Guid Id,
    Guid ClientId,
    string SerialNumber,
    string Model,
    string Manufacturer,
    DateOnly InstallationDate,
    DateTimeOffset? ArchivedAt);
