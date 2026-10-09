using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.WorkOrders;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class ApiMappingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_Keep_Price_From_Time_Of_Use_When_Service_Entry_Is_Mapped()
    {
        var part = new ServiceEntryPartResponse("FP-100", Guid.NewGuid(), "Filtr", 2, 12.34m);
        var response = new ServiceEntryResponse(
            Now, Guid.NewGuid(), false, 50.06, 19.94, "Wymiana filtra", [part], [], Guid.NewGuid(), "Jan Kowalski", Now, Guid.NewGuid(), Now.AddHours(-1));

        var entry = ServiceEntryMapper.FromResponse(response);

        entry.Parts.ShouldBe([new ServiceEntryPart(part.PartId, "Filtr", "FP-100", 2, 12.34m)]);
        entry.Latitude.ShouldBe(50.06);
        entry.WorkStartedAt.ShouldBe(Now.AddHours(-1));
    }
}
