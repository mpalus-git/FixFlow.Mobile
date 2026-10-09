using System.Net;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.WorkOrders;
using Refit;

namespace FixFlow.Mobile.Core.Features.Sync.Pull;

public sealed class WorkOrderDetailsSyncService(
    IFixFlowApi api,
    WorkOrderRepository workOrders,
    DeviceRepository devices,
    ServiceEntryRepository serviceEntries,
    DeviceHistoryRepository deviceHistory,
    WorkOrderEventRepository workOrderEvents)
{
    public const int HistoryPageSize = 100;

    public async Task<PullResult> PullAsync(Guid workOrderId, CancellationToken cancellationToken)
    {
        using var workOrderResponse = await api.GetWorkOrderAsync(workOrderId, cancellationToken);
        if (workOrderResponse is not { IsSuccessful: true, Content: { } content })
        {
            var error = ApiErrorMapper.FromResponse(workOrderResponse);
            if (error.StatusCode == HttpStatusCode.NotFound)
            {
                await serviceEntries.ReplaceForWorkOrderAsync(workOrderId, []);
                await workOrderEvents.ReplaceForWorkOrderAsync(workOrderId, []);
                await workOrders.DeleteAsync(workOrderId);
                return PullResult.NotFound;
            }

            return PullResult.Failed(error);
        }

        var workOrder = WorkOrderMapper.FromResponse(content);
        await workOrders.UpsertAsync(workOrder);

        var deviceTask = api.GetDeviceAsync(workOrder.DeviceId, cancellationToken);
        var entriesTask = api.ListServiceEntriesAsync(workOrderId, cancellationToken);
        var historyTask = api.ListDeviceWorkOrdersAsync(workOrder.DeviceId, 1, HistoryPageSize, cancellationToken);
        var eventsTask = api.ListWorkOrderEventsAsync(workOrderId, cancellationToken);
        await Task.WhenAll(deviceTask, entriesTask, historyTask, eventsTask);

        using var deviceResponse = await deviceTask;
        using var entriesResponse = await entriesTask;
        using var historyResponse = await historyTask;
        using var eventsResponse = await eventsTask;

        if (deviceResponse.Content is { } device)
        {
            await devices.UpsertAsync(WorkOrderMapper.ToDevice(device));
        }

        if (entriesResponse.Content is { } entries)
        {
            await serviceEntries.ReplaceForWorkOrderAsync(workOrderId, [.. entries.Select(ServiceEntryMapper.FromResponse)]);
        }

        if (historyResponse.Content is { } history)
        {
            await deviceHistory.ReplaceForDeviceAsync(
                workOrder.DeviceId,
                [.. history.Items.Select(item => WorkOrderMapper.ToHistoryItem(workOrder.DeviceId, item))]);
        }

        if (eventsResponse.Content is { } events)
        {
            await workOrderEvents.ReplaceForWorkOrderAsync(workOrderId, [.. events.Select(item => WorkOrderMapper.ToEvent(workOrderId, item))]);
        }

        IApiResponse[] responses = [deviceResponse, entriesResponse, historyResponse, eventsResponse];
        return responses.FirstOrDefault(response => !response.IsSuccessful) is { } failed
            ? PullResult.Failed(ApiErrorMapper.FromResponse(failed))
            : PullResult.Succeeded;
    }
}
