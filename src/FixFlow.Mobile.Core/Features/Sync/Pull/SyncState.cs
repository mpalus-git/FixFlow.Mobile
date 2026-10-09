namespace FixFlow.Mobile.Core.Features.Sync.Pull;

public sealed record SyncState(string Key, string Value, DateTimeOffset UpdatedAt);
