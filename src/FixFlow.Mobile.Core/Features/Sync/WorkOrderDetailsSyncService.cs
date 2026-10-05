using System.Net;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.WorkOrders;
using Refit;

namespace FixFlow.Mobile.Core.Features.Sync;

public sealed class WorkOrderDetailsSyncService(
    IFixFlowApi api,
    WorkOrderRepository workOrders,
    ClientRepository clients,
    DeviceRepository devices,
    ServiceEntryRepository serviceEntries,
    DeviceHistoryRepository deviceHistory)
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
                await workOrders.DeleteAsync(workOrderId);
                return PullResult.NotFound;
            }

            return PullResult.Failed(error);
        }

        var workOrder = WorkOrderMapper.FromResponse(content);
        await workOrders.UpsertAsync(workOrder);

        var clientTask = api.GetClientAsync(workOrder.ClientId, cancellationToken);
        var deviceTask = api.GetDeviceAsync(workOrder.DeviceId, cancellationToken);
        var entriesTask = api.ListServiceEntriesAsync(workOrderId, cancellationToken);
        var historyTask = api.ListDeviceWorkOrdersAsync(workOrder.DeviceId, 1, HistoryPageSize, cancellationToken);
        await Task.WhenAll(clientTask, deviceTask, entriesTask, historyTask);

        using var clientResponse = await clientTask;
        using var deviceResponse = await deviceTask;
        using var entriesResponse = await entriesTask;
        using var historyResponse = await historyTask;

        if (clientResponse.Content is { } client)
        {
            await clients.UpsertAsync(WorkOrderMapper.ToClient(client));
        }

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

        IApiResponse[] responses = [clientResponse, deviceResponse, entriesResponse, historyResponse];
        return responses.FirstOrDefault(response => !response.IsSuccessful) is { } failed
            ? PullResult.Failed(ApiErrorMapper.FromResponse(failed))
            : PullResult.Succeeded;
    }
}
