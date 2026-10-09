namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed record ClientContact(string Street, string BuildingNumber, string PostalCode, string City, string ContactPerson, string Phone)
{
    public static ClientContact Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    public bool IsEmpty => this == Empty;
}
