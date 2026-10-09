using FixFlow.Mobile.Core.Features.ServiceEntries;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed record WorkOrderDetailsView(
    string Number,
    string StatusText,
    BadgeTone StatusTone,
    string PriorityText,
    BadgeTone PriorityTone,
    bool IsUrgent,
    bool IsOverdue,
    string DueText,
    string Description,
    ClientView? Client,
    DeviceView? Device,
    IReadOnlyList<ServiceEntryView> Entries,
    string? PartsTotalText,
    IReadOnlyList<DeviceHistoryView> History,
    IReadOnlyList<WorkOrderEventView> Events,
    EntryPhotoView? ClientSignature,
    bool CanDownloadProtocol)
{
    public bool HasAttentionBadges => IsUrgent || IsOverdue;

    public bool HasClientSignature => ClientSignature is not null;

    public bool HasEvents => Events.Count > 0;

    public bool HasClient => Client is not null;

    public bool HasDevice => Device is not null;

    public bool HasEntries => Entries.Count > 0;

    public bool HasHistory => History.Count > 0;

    public bool HasNoEntries => !HasEntries;

    public bool HasNoHistory => !HasHistory;

    public bool HasPartsTotal => PartsTotalText is not null;
}
