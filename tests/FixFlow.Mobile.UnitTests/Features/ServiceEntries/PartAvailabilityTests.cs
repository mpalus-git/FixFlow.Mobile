using FixFlow.Mobile.Core.Features.Parts;
using FixFlow.Mobile.Core.Features.ServiceEntries;

namespace FixFlow.Mobile.UnitTests.Features.ServiceEntries;

public sealed class PartAvailabilityTests
{
    private static readonly Part Filter = new(Guid.NewGuid(), "Filtr", "FP-100", 10, 12.34m, null);

    [Fact]
    public void Should_Return_Last_Known_Stock_When_Nothing_Waits_In_Queue()
    {
        PartAvailability.Available(Filter, []).ShouldBe(10);
    }

    [Fact]
    public void Should_Subtract_Queued_Usage_And_Add_Queued_Returns_When_Entries_Wait_In_Queue()
    {
        PendingServiceEntry[] pending =
        [
            Entry(isCorrection: false, new PendingServiceEntryPart(Filter.Id, "Filtr", "FP-100", 4, 12.34m)),
            Entry(isCorrection: false, new PendingServiceEntryPart(Guid.NewGuid(), "Inna", "IN-1", 7, 1m)),
            Entry(isCorrection: true, new PendingServiceEntryPart(Filter.Id, "Filtr", "FP-100", 1, 12.34m)),
        ];

        PartAvailability.Available(Filter, pending).ShouldBe(7);
    }

    private static PendingServiceEntry Entry(bool isCorrection, PendingServiceEntryPart part) =>
        new(Guid.CreateVersion7(), "Wpis", isCorrection, null, null, null, null, [part]);
}
