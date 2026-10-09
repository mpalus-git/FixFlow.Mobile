using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Screens;

public sealed record StatusBannerView(string Text, StatusTone Tone, bool HasQueueAction)
{
    private const string Separator = "  ·  ";

    public static StatusBannerView? Compose(string? offlineMessage, ScreenNotice? notice, int pendingChanges, int rejectedChanges)
    {
        string[] queue =
        [
            .. pendingChanges > 0 ? [WorkOrderTexts.Format(AppResources.SyncPendingCount, pendingChanges)] : Array.Empty<string>(),
            .. rejectedChanges > 0 ? [WorkOrderTexts.Format(AppResources.SyncConflictCount, rejectedChanges)] : Array.Empty<string>(),
        ];
        string[] lines =
        [
            .. offlineMessage is null ? Array.Empty<string>() : [offlineMessage],
            .. notice is null ? Array.Empty<string>() : [notice.Message],
            .. queue.Length == 0 ? Array.Empty<string>() : [string.Join(Separator, queue)],
        ];
        if (lines.Length == 0)
        {
            return null;
        }

        var tone = rejectedChanges > 0 ? StatusTone.Destructive : notice?.Tone ?? StatusTone.Neutral;
        return new StatusBannerView(string.Join('\n', lines), tone, queue.Length > 0);
    }
}
