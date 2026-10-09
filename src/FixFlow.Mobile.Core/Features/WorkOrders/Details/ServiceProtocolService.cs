using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Launching;

namespace FixFlow.Mobile.Core.Features.WorkOrders.Details;

public sealed class ServiceProtocolService(IFixFlowApi api, IDocumentLauncher documents)
{
    public const string PdfContentType = "application/pdf";

    public async Task<ApiError?> OpenAsync(Guid workOrderId, string workOrderNumber, CancellationToken cancellationToken)
    {
        using var response = await api.GetServiceProtocolAsync(workOrderId, cancellationToken);
        if (response is not { IsSuccessful: true, Content: { } content })
        {
            return ApiErrorMapper.FromResponse(response);
        }

        var disposition = response.ContentHeaders?.ContentDisposition;
        var fileName = disposition?.FileNameStar ?? disposition?.FileName?.Trim('"') ?? $"protokol-{workOrderNumber}.pdf";
        await documents.OpenAsync(content, fileName, PdfContentType);
        return null;
    }
}
