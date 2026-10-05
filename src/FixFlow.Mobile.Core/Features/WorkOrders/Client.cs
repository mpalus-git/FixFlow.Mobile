namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed record Client(
    Guid Id,
    string Name,
    string Street,
    string BuildingNumber,
    string PostalCode,
    string City,
    string ContactPerson,
    string Phone,
    string? Email,
    DateTimeOffset? ArchivedAt);
