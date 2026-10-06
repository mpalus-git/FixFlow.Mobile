using System.Globalization;
using System.Net;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using Refit;

namespace FixFlow.Mobile.Core.Features.Sync;

public sealed class OutboxDispatcher(
    IFixFlowApi api,
    OutboxRepository outbox,
    ServiceEntryRepository serviceEntries,
    OutboxRetryPolicy retryPolicy,
    TimeProvider timeProvider)
{
    public async Task<DispatchSummary> DispatchAsync(CancellationToken cancellationToken)
    {
        var blockedWorkOrders = (await outbox.GetConflictsAsync()).Select(conflict => conflict.WorkOrderId).ToHashSet();
        var operations = await outbox.GetAllAsync();
        int sent = 0, rejected = 0;

        foreach (var workOrderQueue in operations.GroupBy(operation => operation.WorkOrderId))
        {
            if (blockedWorkOrders.Contains(workOrderQueue.Key))
            {
                continue;
            }

            foreach (var operation in workOrderQueue)
            {
                if (operation.NextAttemptAt > timeProvider.GetUtcNow())
                {
                    break;
                }

                var error = await SendAsync(operation, cancellationToken);
                if (error is null)
                {
                    await outbox.RemoveAsync(operation.Id);
                    sent++;
                    continue;
                }

                if (error.Kind == ApiErrorKind.Unauthorized)
                {
                    return new DispatchSummary(sent, rejected, operations.Count - sent - rejected, SessionRejected: true);
                }

                if (error.ShouldRetry)
                {
                    var nextAttemptAt = timeProvider.GetUtcNow() + retryPolicy.NextDelay(operation.AttemptCount + 1, error.RetryAfter);
                    await outbox.RecordFailedAttemptAsync(operation.Id, nextAttemptAt, Describe(error));
                }
                else
                {
                    await outbox.MoveToConflictsAsync(new SyncConflict(
                        operation.Id,
                        operation.WorkOrderId,
                        operation.Kind,
                        operation.Payload,
                        operation.CreatedAt,
                        error.StatusCode,
                        error.ErrorCode,
                        error.Detail ?? string.Join(Environment.NewLine, error.FieldErrors.Values.SelectMany(messages => messages)),
                        timeProvider.GetUtcNow()));
                    rejected++;
                }

                break;
            }
        }

        return new DispatchSummary(sent, rejected, operations.Count - sent - rejected, SessionRejected: false);
    }

    private static string Describe(ApiError error) =>
        error.Detail ?? (error.StatusCode is { } status ? ((int)status).ToString(CultureInfo.InvariantCulture) : "network");

    private async Task<ApiError?> SendAsync(OutboxOperation operation, CancellationToken cancellationToken)
    {
        using var response = await CallAsync(operation, cancellationToken);
        if (response.IsSuccessful)
        {
            if (response is IApiResponse<ServiceEntryResponse> { Content: { } entry })
            {
                await serviceEntries.UpsertAsync(ServiceEntryMapper.FromResponse(entry));
            }

            return null;
        }

        var error = ApiErrorMapper.FromResponse(response);
        return error.StatusCode == HttpStatusCode.Conflict && await IsAlreadyAppliedAsync(operation, cancellationToken)
            ? null
            : error;
    }

    private async Task<IApiResponse> CallAsync(OutboxOperation operation, CancellationToken cancellationToken) => operation.Kind switch
    {
        OutboxOperationKind.StartWork => await api.StartWorkAsync(
            operation.WorkOrderId, new StartWorkRequest(OutboxPayload.StartedAt(operation)), cancellationToken),
        OutboxOperationKind.CompleteWorkOrder => await api.CompleteWorkOrderAsync(operation.WorkOrderId, cancellationToken),
        _ => await api.AddServiceEntryAsync(operation.WorkOrderId, ToRequest(OutboxPayload.ToServiceEntry(operation)), cancellationToken),
    };

    private async Task<bool> IsAlreadyAppliedAsync(OutboxOperation operation, CancellationToken cancellationToken)
    {
        if (operation.Kind == OutboxOperationKind.AddServiceEntry)
        {
            return false;
        }

        using var response = await api.GetWorkOrderAsync(operation.WorkOrderId, cancellationToken);
        return (operation.Kind, response.Content?.Status) switch
        {
            (OutboxOperationKind.StartWork, WorkOrderStatus.InProgress) => true,
            (OutboxOperationKind.CompleteWorkOrder, WorkOrderStatus.Completed or WorkOrderStatus.Invoiced) => true,
            _ => false,
        };
    }

    private static AddServiceEntryRequest ToRequest(PendingServiceEntry entry) => new(
        entry.Id,
        entry.IsCorrection,
        entry.Latitude,
        entry.Longitude,
        entry.Note,
        [.. entry.Parts.Select(part => new ServiceEntryPartRequest(part.PartId, part.Quantity))],
        null,
        entry.WorkFinishedAt,
        entry.WorkStartedAt);
}
