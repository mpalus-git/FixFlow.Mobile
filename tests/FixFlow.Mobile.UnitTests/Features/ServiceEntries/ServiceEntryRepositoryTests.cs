using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.UnitTests.Storage;

namespace FixFlow.Mobile.UnitTests.Features.ServiceEntries;

public sealed class ServiceEntryRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkOrderId = Guid.NewGuid();

    private readonly TestUserDatabase _userDatabase = new();
    private readonly ServiceEntryRepository _repository;

    public ServiceEntryRepositoryTests()
    {
        _repository = new ServiceEntryRepository(_userDatabase.Database);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Keep_Parts_In_Order_With_Price_From_Time_Of_Use_When_Entry_Is_Stored()
    {
        var entry = CreateEntry(Now, new ServiceEntryPart(Guid.NewGuid(), "Filtr", "FP-100", 2, 12.34m), new ServiceEntryPart(Guid.NewGuid(), "Czujnik", "CZ-1", 1, 0.1m + 0.2m));

        await _repository.ReplaceForWorkOrderAsync(WorkOrderId, [entry]);

        var stored = (await _repository.GetForWorkOrderAsync(WorkOrderId)).ShouldHaveSingleItem();
        ShouldMatch(stored, entry);
        stored.Parts[0].Amount.ShouldBe(24.68m);
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();

    private static void ShouldMatch(ServiceEntry actual, ServiceEntry expected)
    {
        (actual with { Parts = [] }).ShouldBe(expected with { Parts = [] });
        actual.Parts.ShouldBe(expected.Parts);
    }

    private static ServiceEntry CreateEntry(DateTimeOffset createdAt, params ServiceEntryPart[] parts) => new(
        Guid.NewGuid(),
        WorkOrderId,
        Guid.NewGuid(),
        "Jan Kowalski",
        "Wymiana filtra",
        false,
        createdAt.AddHours(-1),
        createdAt,
        50.0614,
        19.9366,
        createdAt,
        parts);
}
