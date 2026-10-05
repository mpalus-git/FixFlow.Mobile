using FixFlow.Mobile.Core.Features.Parts;
using FixFlow.Mobile.UnitTests.Storage;

namespace FixFlow.Mobile.UnitTests.Features.Parts;

public sealed class PartRepositoryTests : IAsyncLifetime
{
    private static readonly Part Filter = CreatePart("Filtr powietrza", "FP-100", 12.34m);
    private static readonly Part Sensor = CreatePart("Czujnik ŁOŻYSKA", "CZ-200", 199.99m);
    private static readonly Part Archived = CreatePart("Filtr stary", "FP-001", 5m) with { ArchivedAt = DateTimeOffset.UnixEpoch };

    private readonly TestUserDatabase _userDatabase = new();
    private readonly PartRepository _repository;

    public PartRepositoryTests()
    {
        _repository = new PartRepository(_userDatabase.Database);
    }

    public async ValueTask InitializeAsync()
    {
        await _userDatabase.Database.OpenAsync(Guid.NewGuid());
        await _repository.ReplaceAllAsync([Filter, Sensor, Archived]);
    }

    [Fact]
    public async Task Should_Keep_Exact_Price_When_Part_Is_Stored_Locally()
    {
        var part = CreatePart("Uszczelka", "U-1", 0.1m + 0.2m);
        await _repository.ReplaceAllAsync([part]);

        (await _repository.GetAsync(part.Id)).ShouldBe(part);
    }

    [Theory]
    [InlineData("filtr")]
    [InlineData("fp-1")]
    [InlineData("  FILTR POW ")]
    public async Task Should_Find_Active_Part_When_Term_Matches_Name_Or_Catalog_Number(string term)
    {
        (await _repository.SearchAsync(term)).ShouldBe([Filter]);
    }

    [Fact]
    public async Task Should_Ignore_Case_Of_Polish_Letters_When_Searching()
    {
        (await _repository.SearchAsync("łożyska")).ShouldBe([Sensor]);
    }

    [Fact]
    public async Task Should_Treat_Wildcards_Literally_When_Term_Contains_Them()
    {
        (await _repository.SearchAsync("%")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Return_Archived_Part_When_Read_By_Id()
    {
        (await _repository.GetAsync(Archived.Id)).ShouldNotBeNull().IsArchived.ShouldBeTrue();
        (await _repository.SearchAsync("stary")).ShouldBeEmpty();
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();

    private static Part CreatePart(string name, string catalogNumber, decimal unitPrice) =>
        new(Guid.NewGuid(), name, catalogNumber, 10, unitPrice, null);
}
