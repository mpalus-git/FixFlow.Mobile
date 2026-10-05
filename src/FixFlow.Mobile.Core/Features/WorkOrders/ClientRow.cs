using SQLite;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

[Table("clients")]
internal sealed class ClientRow
{
    [PrimaryKey]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("street")]
    public string Street { get; set; } = string.Empty;

    [Column("building_number")]
    public string BuildingNumber { get; set; } = string.Empty;

    [Column("postal_code")]
    public string PostalCode { get; set; } = string.Empty;

    [Column("city")]
    public string City { get; set; } = string.Empty;

    [Column("contact_person")]
    public string ContactPerson { get; set; } = string.Empty;

    [Column("phone")]
    public string Phone { get; set; } = string.Empty;

    [Column("email")]
    public string? Email { get; set; }

    [Column("archived_at")]
    public DateTimeOffset? ArchivedAt { get; set; }

    public static ClientRow From(Client client) => new()
    {
        Id = client.Id,
        Name = client.Name,
        Street = client.Street,
        BuildingNumber = client.BuildingNumber,
        PostalCode = client.PostalCode,
        City = client.City,
        ContactPerson = client.ContactPerson,
        Phone = client.Phone,
        Email = client.Email,
        ArchivedAt = client.ArchivedAt,
    };

    public Client ToClient() => new(Id, Name, Street, BuildingNumber, PostalCode, City, ContactPerson, Phone, Email, ArchivedAt);
}
