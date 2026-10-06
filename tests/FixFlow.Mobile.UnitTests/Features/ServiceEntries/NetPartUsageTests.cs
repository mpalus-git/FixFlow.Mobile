using FixFlow.Mobile.Core.Features.ServiceEntries;

namespace FixFlow.Mobile.UnitTests.Features.ServiceEntries;

public sealed class NetPartUsageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid FilterId = Guid.NewGuid();
    private static readonly Guid BeltId = Guid.NewGuid();

    [Fact]
    public void Should_Sum_Work_Entries_And_Subtract_Corrections_With_Price_Of_Last_Use()
    {
        ServiceEntry[] stored =
        [
            Stored(false, new ServiceEntryPart(FilterId, "Filtr", "FP-100", 3, 10m)),
            Stored(false, new ServiceEntryPart(FilterId, "Filtr", "FP-100", 2, 12m), new ServiceEntryPart(BeltId, "Pasek", "PN-200", 1, 45m)),
            Stored(true, new ServiceEntryPart(FilterId, "Filtr", "FP-100", 1, 12m)),
        ];

        var usage = NetPartUsage.Calculate(stored, []);

        usage.ShouldBe([new NetPartUsage(FilterId, "Filtr", "FP-100", 4, 12m), new NetPartUsage(BeltId, "Pasek", "PN-200", 1, 45m)]);
    }

    [Fact]
    public void Should_Include_Queued_Entries_And_Skip_Parts_Fully_Returned()
    {
        ServiceEntry[] stored = [Stored(false, new ServiceEntryPart(BeltId, "Pasek", "PN-200", 1, 45m))];
        PendingServiceEntry[] pending =
        [
            Pending(false, new PendingServiceEntryPart(FilterId, "Filtr", "FP-100", 2, 11m)),
            Pending(true, new PendingServiceEntryPart(BeltId, "Pasek", "PN-200", 1, 45m)),
        ];

        NetPartUsage.Calculate(stored, pending).ShouldBe([new NetPartUsage(FilterId, "Filtr", "FP-100", 2, 11m)]);
    }

    private static ServiceEntry Stored(bool isCorrection, params ServiceEntryPart[] parts) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Jan", "Wpis", isCorrection, null, null, null, null, Now, parts);

    private static PendingServiceEntry Pending(bool isCorrection, params PendingServiceEntryPart[] parts) =>
        new(Guid.CreateVersion7(), "Wpis", isCorrection, null, null, null, null, parts);
}
