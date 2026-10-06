using FixFlow.Mobile.Core.Features.Parts;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.ServiceEntries;

public sealed class ServiceEntryPartsSelectionTests : IAsyncLifetime
{
    private static readonly Part Filter = new(Guid.NewGuid(), "Filtr powietrza", "FP-100", 3, 12.34m, null);
    private static readonly Part Belt = new(Guid.NewGuid(), "Pasek napędowy", "PN-200", 0, 45m, null);

    private readonly TestUserDatabase _userDatabase = new();
    private readonly PartRepository _parts;
    private readonly ServiceEntryPartsSelection _selection;

    public ServiceEntryPartsSelectionTests()
    {
        _parts = new PartRepository(_userDatabase.Database);
        var formatter = new DisplayFormatter(new LanguageService(Substitute.For<ISettingsStore>()), BusinessClock.CreateForWarsaw(new FakeTimeProvider()));
        _selection = new ServiceEntryPartsSelection(_parts, formatter);
    }

    public async ValueTask InitializeAsync()
    {
        await _userDatabase.Database.OpenAsync(Guid.NewGuid());
        await _parts.ReplaceAllAsync([Filter, Belt]);
    }

    [Fact]
    public async Task Should_List_Nothing_Until_Technician_Types_Search_Text()
    {
        await _selection.LoadAsync([]);

        _selection.Results.ShouldBeEmpty();
        _selection.HasNoResults.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Show_Availability_Reduced_By_Queued_Entries_When_Catalog_Is_Searched()
    {
        await _selection.LoadAsync([Pending(Filter, 1)]);
        _selection.SearchText = "-";
        await _selection.SearchAsync();

        var filter = _selection.Results.Single(result => result.Part.Id == Filter.Id);
        filter.Available.ShouldBe(2);
        filter.AvailabilityText.ShouldBe(WorkOrderTexts.Format(AppResources.EntryPartAvailable, 2));
        var belt = _selection.Results.Single(result => result.Part.Id == Belt.Id);
        belt.CanAdd.ShouldBeFalse();
        belt.AvailabilityText.ShouldBe(AppResources.EntryPartOutOfStock);
    }

    [Fact]
    public async Task Should_Find_Part_By_Fragment_Of_Catalog_Number_When_Searching_Offline()
    {
        await _selection.LoadAsync([]);

        _selection.SearchText = "pn-2";
        await _selection.SearchAsync();

        _selection.Results.ShouldHaveSingleItem().Part.Id.ShouldBe(Belt.Id);
    }

    [Fact]
    public async Task Should_Move_Part_To_Selection_With_One_Unit_When_Added()
    {
        await _selection.LoadAsync([]);
        _selection.SearchText = "-";
        await _selection.SearchAsync();

        await _selection.AddCommand.ExecuteAsync(_selection.Results.Single(result => result.Part.Id == Filter.Id));

        var draft = _selection.Selected.ShouldHaveSingleItem();
        draft.Quantity.ShouldBe(1);
        _selection.HasSelected.ShouldBeTrue();
        _selection.Results.ShouldNotContain(result => result.Part.Id == Filter.Id);
    }

    [Fact]
    public async Task Should_Not_Add_Part_When_It_Is_Out_Of_Stock()
    {
        await _selection.LoadAsync([]);
        _selection.SearchText = "-";
        await _selection.SearchAsync();

        await _selection.AddCommand.ExecuteAsync(_selection.Results.Single(result => result.Part.Id == Belt.Id));

        _selection.Selected.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Report_Quantity_Above_Stock_When_Catalog_Changed_After_Selection()
    {
        await _selection.LoadAsync([]);
        _selection.SearchText = "-";
        await _selection.SearchAsync();
        await _selection.AddCommand.ExecuteAsync(_selection.Results.Single(result => result.Part.Id == Filter.Id));
        _selection.Selected[0].IncreaseCommand.Execute(null);

        await _parts.ReplaceAllAsync([Filter with { StockQuantity = 1 }]);

        (await _selection.ValidateAsync()).ShouldBe(WorkOrderTexts.Format(AppResources.EntryQuantityExceedsStock, Filter.Name, 1));
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();

    private static PendingServiceEntry Pending(Part part, int quantity) =>
        new(Guid.CreateVersion7(), "Wpis", false, null, null, null, null, [new PendingServiceEntryPart(part.Id, part.Name, part.CatalogNumber, quantity, part.UnitPrice)]);
}
