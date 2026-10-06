using FixFlow.Mobile.Core.Features.ServiceEntries;

namespace FixFlow.Mobile.UnitTests.Features.ServiceEntries;

public sealed class ReturnedPartDraftTests
{
    private static readonly NetPartUsage Filter = new(Guid.NewGuid(), "Filtr", "FP-100", 3, 12m);

    [Fact]
    public void Should_Not_Return_More_Than_Net_Quantity_When_Increasing()
    {
        var draft = new ReturnedPartDraft(Filter, "FP-100");

        for (var step = 0; step < 5; step++)
        {
            draft.IncreaseCommand.Execute(null);
        }

        draft.Quantity.ShouldBe(3);
        draft.IncreaseCommand.CanExecute(null).ShouldBeFalse();
        draft.ToPending().ShouldBe(new PendingServiceEntryPart(Filter.PartId, "Filtr", "FP-100", 3, 12m));
    }

    [Fact]
    public void Should_Start_At_Zero_And_Not_Go_Below_It_When_Decreasing()
    {
        var draft = new ReturnedPartDraft(Filter, "FP-100");

        draft.DecreaseCommand.Execute(null);

        draft.Quantity.ShouldBe(0);
        draft.DecreaseCommand.CanExecute(null).ShouldBeFalse();
    }
}
