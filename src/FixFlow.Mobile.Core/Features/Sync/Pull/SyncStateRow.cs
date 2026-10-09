using SQLite;

namespace FixFlow.Mobile.Core.Features.Sync.Pull;

[Table("sync_states")]
internal sealed class SyncStateRow
{
    [PrimaryKey]
    [Column("key")]
    public string Key { get; set; } = string.Empty;

    [Column("value")]
    public string Value { get; set; } = string.Empty;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
