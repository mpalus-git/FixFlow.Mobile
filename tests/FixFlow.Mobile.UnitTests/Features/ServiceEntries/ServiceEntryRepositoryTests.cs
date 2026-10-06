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

    [Fact]
    public async Task Should_Keep_Photo_Addresses_When_Entry_Is_Stored()
    {
        var entry = CreateEntry(Now) with { PhotoUrls = ["https://api.test/api/v1/photos/1", "https://upload.wikimedia.org/a.jpg"] };

        await _repository.UpsertAsync(entry);

        (await _repository.GetForWorkOrderAsync(WorkOrderId)).ShouldHaveSingleItem().PhotoUrls.ShouldBe(entry.PhotoUrls);
    }

    [Fact]
    public async Task Should_Replace_Entries_Of_Work_Order_Only_When_Server_Returns_New_List()
    {
        var otherWorkOrderEntry = CreateEntry(Now) with { WorkOrderId = Guid.NewGuid() };
        await _repository.ReplaceForWorkOrderAsync(otherWorkOrderEntry.WorkOrderId, [otherWorkOrderEntry]);
        await _repository.ReplaceForWorkOrderAsync(WorkOrderId, [CreateEntry(Now, new ServiceEntryPart(Guid.NewGuid(), "Stara", "S-1", 1, 1m))]);

        var later = CreateEntry(Now.AddHours(2));
        var earlier = CreateEntry(Now.AddHours(1)) with { IsCorrection = true, WorkStartedAt = null, WorkFinishedAt = null, Latitude = null, Longitude = null };
        await _repository.ReplaceForWorkOrderAsync(WorkOrderId, [later, earlier]);

        var stored = await _repository.GetForWorkOrderAsync(WorkOrderId);
        stored.Count.ShouldBe(2);
        ShouldMatch(stored[0], earlier);
        ShouldMatch(stored[1], later);
        ShouldMatch((await _repository.GetForWorkOrderAsync(otherWorkOrderEntry.WorkOrderId)).ShouldHaveSingleItem(), otherWorkOrderEntry);
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();

    private static void ShouldMatch(ServiceEntry actual, ServiceEntry expected)
    {
        (actual with { Parts = [], PhotoUrls = [] }).ShouldBe(expected with { Parts = [], PhotoUrls = [] });
        actual.PhotoUrls.ShouldBe(expected.PhotoUrls);
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
