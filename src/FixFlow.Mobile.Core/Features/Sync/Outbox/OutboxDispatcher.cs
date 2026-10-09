using System.Globalization;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync.Conflicts;
using Refit;

namespace FixFlow.Mobile.Core.Features.Sync.Outbox;

public sealed class OutboxDispatcher(
    IFixFlowApi api,
    OutboxRepository outbox,
    ServiceEntryRepository serviceEntries,
    PhotoUploadClient photoUploads,
    PhotoStore photos,
    OutboxRetryPolicy retryPolicy,
    TimeProvider timeProvider)
{
    public const string MissingPhotoFileCode = "Local.PhotoFileMissing";

    private static readonly ApiError MissingPhotoFile =
        new(ApiErrorKind.Business, null, MissingPhotoFileCode, null, new Dictionary<string, IReadOnlyList<string>>(), null);

    public async Task<DispatchSummary> DispatchAsync(CancellationToken cancellationToken)
    {
        var blockedWorkOrders = (await outbox.GetConflictsAsync()).Select(conflict => conflict.WorkOrderId).ToHashSet();
        var operations = await outbox.GetAllAsync();
        int sent = 0, rejected = 0;
        DateTimeOffset? nextAttemptAt = null;
        var changedPartStock = false;

        foreach (var workOrderQueue in operations.GroupBy(operation => operation.WorkOrderId))
        {
            if (blockedWorkOrders.Contains(workOrderQueue.Key))
            {
                continue;
            }

            foreach (var operation in workOrderQueue)
            {
                if (operation.NextAttemptAt is { } waitingUntil && waitingUntil > timeProvider.GetUtcNow())
                {
                    nextAttemptAt = Earliest(nextAttemptAt, waitingUntil);
                    break;
                }

                var error = await SendAsync(operation, cancellationToken);
                if (error is null)
                {
                    await outbox.RemoveAsync(operation.Id);
                    if (operation.Kind == OutboxOperationKind.UploadPhoto)
                    {
                        photos.Delete(OutboxPayload.PhotoId(operation));
                    }

                    changedPartStock |= operation.Kind == OutboxOperationKind.AddServiceEntry && OutboxPayload.ToServiceEntry(operation).Parts.Count > 0;
                    sent++;
                    continue;
                }

                if (error.Kind == ApiErrorKind.Unauthorized)
                {
                    return new DispatchSummary(sent, rejected, operations.Count - sent - rejected, SessionRejected: true) { ChangedPartStock = changedPartStock };
                }

                if (error.ShouldRetry)
                {
                    var retryAt = timeProvider.GetUtcNow() + retryPolicy.NextDelay(operation.AttemptCount + 1, error.RetryAfter);
                    await outbox.RecordFailedAttemptAsync(operation.Id, retryAt, Describe(error));
                    nextAttemptAt = Earliest(nextAttemptAt, retryAt);
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

        return new DispatchSummary(sent, rejected, operations.Count - sent - rejected, SessionRejected: false)
        {
            NextAttemptAt = nextAttemptAt,
            ChangedPartStock = changedPartStock,
        };
    }

    private static DateTimeOffset Earliest(DateTimeOffset? current, DateTimeOffset candidate) =>
        current is { } earlier && earlier < candidate ? earlier : candidate;

    private static string Describe(ApiError error) =>
        error.Detail ?? (error.StatusCode is { } status ? ((int)status).ToString(CultureInfo.InvariantCulture) : "network");

    private async Task<ApiError?> SendAsync(OutboxOperation operation, CancellationToken cancellationToken)
    {
        if (operation.Kind == OutboxOperationKind.UploadPhoto)
        {
            var photoId = OutboxPayload.PhotoId(operation);
            return await photos.ReadAsync(photoId) is { } jpeg
                ? await photoUploads.UploadAsync(photoId, jpeg, cancellationToken)
                : MissingPhotoFile;
        }

        using var response = await CallAsync(operation, cancellationToken);
        if (response.IsSuccessful)
        {
            if (response is IApiResponse<ServiceEntryResponse> { Content: { } entry })
            {
                await serviceEntries.UpsertAsync(ServiceEntryMapper.FromResponse(entry));
            }

            return null;
        }

        return ApiErrorMapper.FromResponse(response);
    }

    private async Task<IApiResponse> CallAsync(OutboxOperation operation, CancellationToken cancellationToken) => operation.Kind switch
    {
        OutboxOperationKind.StartWork => await api.StartWorkAsync(
            operation.WorkOrderId, new StartWorkRequest(OutboxPayload.StartedAt(operation)), cancellationToken),
        OutboxOperationKind.CompleteWorkOrder => await api.CompleteWorkOrderAsync(
            operation.WorkOrderId, new CompleteWorkOrderRequest(OutboxPayload.ClientSignaturePhotoId(operation), OutboxPayload.CompletedAt(operation)), cancellationToken),
        _ => await api.AddServiceEntryAsync(operation.WorkOrderId, ToRequest(OutboxPayload.ToServiceEntry(operation)), cancellationToken),
    };

    private static AddServiceEntryRequest ToRequest(PendingServiceEntry entry) => new(
        entry.Id,
        entry.IsCorrection,
        entry.Latitude,
        entry.Longitude,
        entry.Note,
        [.. entry.Parts.Select(part => new ServiceEntryPartRequest(part.PartId, part.Quantity))],
        entry.Photos.Count == 0 ? null : [.. entry.Photos.Select(photo => photo.Url)],
        entry.WorkFinishedAt,
        entry.WorkStartedAt);
}
