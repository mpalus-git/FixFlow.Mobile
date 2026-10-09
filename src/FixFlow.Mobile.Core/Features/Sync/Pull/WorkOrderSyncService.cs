using System.Globalization;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Features.WorkOrders;

namespace FixFlow.Mobile.Core.Features.Sync.Pull;

public sealed class WorkOrderSyncService(
    IFixFlowApi api,
    WorkOrderRepository workOrders,
    SyncStateRepository syncStates)
{
    public const string SyncStateKey = "work-orders";

    public const int PageSize = 100;

    public async Task<DateTimeOffset?> GetLastPulledAtAsync() => (await syncStates.GetAsync(SyncStateKey))?.UpdatedAt;

    public async Task<PullResult> PullAsync(CancellationToken cancellationToken)
    {
        var pulled = new List<WorkOrder>();
        for (var page = 1; ; page++)
        {
            using var response = await api.ListWorkOrdersAsync(
                page, PageSize, null, null, null, null, null, null, null, null, SortBy.DueDate, SortDirection.Asc, cancellationToken);
            if (response is not { IsSuccessful: true, Content: { } content })
            {
                return PullResult.Failed(ApiErrorMapper.FromResponse(response));
            }

            pulled.AddRange(content.Items.Select(WorkOrderMapper.FromListItem));
            if (content.Items.Count == 0 || page * PageSize >= content.TotalCount)
            {
                break;
            }
        }

        await workOrders.ReplaceAllAsync(pulled);
        await syncStates.SetAsync(SyncStateKey, pulled.Count.ToString(CultureInfo.InvariantCulture));
        return PullResult.Succeeded;
    }
}
