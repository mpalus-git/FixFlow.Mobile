using System.Globalization;
using SQLite;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

[Table("devices")]
internal sealed class DeviceRow
{
    private const string DateFormat = "yyyy-MM-dd";

    [PrimaryKey]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("client_id")]
    public Guid ClientId { get; set; }

    [Column("serial_number")]
    public string SerialNumber { get; set; } = string.Empty;

    [Column("model")]
    public string Model { get; set; } = string.Empty;

    [Column("manufacturer")]
    public string Manufacturer { get; set; } = string.Empty;

    [Column("installation_date")]
    public string InstallationDate { get; set; } = string.Empty;

    [Column("archived_at")]
    public DateTimeOffset? ArchivedAt { get; set; }

    public static DeviceRow From(Device device) => new()
    {
        Id = device.Id,
        ClientId = device.ClientId,
        SerialNumber = device.SerialNumber,
        Model = device.Model,
        Manufacturer = device.Manufacturer,
        InstallationDate = device.InstallationDate.ToString(DateFormat, CultureInfo.InvariantCulture),
        ArchivedAt = device.ArchivedAt,
    };

    public Device ToDevice() => new(
        Id,
        ClientId,
        SerialNumber,
        Model,
        Manufacturer,
        DateOnly.ParseExact(InstallationDate, DateFormat, CultureInfo.InvariantCulture),
        ArchivedAt);
}
