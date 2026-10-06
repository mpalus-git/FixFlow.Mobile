using System.Globalization;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Features.Parts;

namespace FixFlow.Mobile.Core.Features.Sync;

public sealed class PartSyncService(IFixFlowApi api, PartRepository parts, SyncStateRepository syncStates)
{
    public const string SyncStateKey = "parts";

    public const int PageSize = 100;

    public async Task<PullResult> PullAsync(CancellationToken cancellationToken)
    {
        var pulled = new List<Part>();
        for (var page = 1; ; page++)
        {
            using var response = await api.ListPartsAsync(page, PageSize, null, cancellationToken);
            if (response is not { IsSuccessful: true, Content: { } content })
            {
                return PullResult.Failed(ApiErrorMapper.FromResponse(response));
            }

            pulled.AddRange(content.Items.Select(PartMapper.FromResponse));
            if (content.Items.Count == 0 || page * PageSize >= content.TotalCount)
            {
                break;
            }
        }

        await parts.ReplaceAllAsync(pulled);
        await syncStates.SetAsync(SyncStateKey, pulled.Count.ToString(CultureInfo.InvariantCulture));
        return PullResult.Succeeded;
    }
}
