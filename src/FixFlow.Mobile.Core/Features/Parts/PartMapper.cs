using FixFlow.Mobile.Core.Api;

namespace FixFlow.Mobile.Core.Features.Parts;

public static class PartMapper
{
    public static Part FromResponse(PartResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new Part(response.Id, response.Name, response.CatalogNumber, response.StockQuantity, response.UnitPrice, response.ArchivedAt);
    }
}
