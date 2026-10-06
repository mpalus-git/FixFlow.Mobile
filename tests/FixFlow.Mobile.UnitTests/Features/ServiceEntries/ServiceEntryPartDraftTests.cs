using FixFlow.Mobile.Core.Features.Parts;
using FixFlow.Mobile.Core.Features.ServiceEntries;

namespace FixFlow.Mobile.UnitTests.Features.ServiceEntries;

public sealed class ServiceEntryPartDraftTests
{
    private static readonly Part Filter = new(Guid.NewGuid(), "Filtr powietrza", "FP-100", 3, 12.34m, null);

    [Fact]
    public void Should_Limit_Quantity_To_Available_Stock_When_Increasing()
    {
        var draft = new ServiceEntryPartDraft(Filter, 3, "FP-100");

        draft.IncreaseCommand.Execute(null);
        draft.IncreaseCommand.Execute(null);

        draft.Quantity.ShouldBe(3);
        draft.IncreaseCommand.CanExecute(null).ShouldBeFalse();
        draft.ToPending().ShouldBe(new PendingServiceEntryPart(Filter.Id, Filter.Name, Filter.CatalogNumber, 3, Filter.UnitPrice));
    }

    [Fact]
    public void Should_Not_Go_Below_One_Unit_When_Decreasing()
    {
        var draft = new ServiceEntryPartDraft(Filter, 3, "FP-100");

        draft.DecreaseCommand.CanExecute(null).ShouldBeFalse();
        draft.IncreaseCommand.Execute(null);
        draft.DecreaseCommand.Execute(null);

        draft.Quantity.ShouldBe(1);
    }

    [Fact]
    public void Should_Cap_Quantity_At_Api_Limit_When_Stock_Is_Larger()
    {
        var draft = new ServiceEntryPartDraft(Filter with { StockQuantity = 5000 }, 5000, "FP-100");

        for (var step = 0; step < 1200; step++)
        {
            draft.IncreaseCommand.Execute(null);
        }

        draft.Quantity.ShouldBe(ServiceEntryPartDraft.MaxQuantity);
    }
}
