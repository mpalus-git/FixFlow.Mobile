using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixFlow.Mobile.Core.Features.Parts;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public sealed partial class ServiceEntryPartsSelection(PartRepository parts, DisplayFormatter formatter) : ObservableObject
{
    public const int MaxParts = 20;

    public const int SearchLimit = 20;

    private IReadOnlyList<PendingServiceEntry> _pendingEntries = [];
    private int _searchVersion;

    public ObservableCollection<ServiceEntryPartDraft> Selected { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoResults))]
    public partial IReadOnlyList<PartSearchResult> Results { get; private set; } = [];

    public bool HasNoResults => HasRoom && !string.IsNullOrWhiteSpace(SearchText) && Results.Count == 0;

    public bool HasSelected => Selected.Count > 0;

    public bool HasRoom => Selected.Count < MaxParts;

    public Task LoadAsync(IReadOnlyList<PendingServiceEntry> pendingEntries)
    {
        _pendingEntries = pendingEntries;
        return SearchAsync();
    }

    public async Task SearchAsync()
    {
        var version = ++_searchVersion;
        IReadOnlyList<Part> found = string.IsNullOrWhiteSpace(SearchText) ? [] : await parts.SearchAsync(SearchText, SearchLimit + Selected.Count);
        if (version == _searchVersion)
        {
            Results = [.. found.Where(part => Selected.All(draft => draft.PartId != part.Id)).Take(SearchLimit).Select(ToResult)];
        }
    }

    public async Task<string?> ValidateAsync()
    {
        foreach (var draft in Selected)
        {
            var available = await parts.GetAsync(draft.PartId) is { IsArchived: false } part ? PartAvailability.Available(part, _pendingEntries) : 0;
            if (draft.Quantity > available)
            {
                return AppText.Format(AppResources.EntryQuantityExceedsStock, draft.Name, Math.Max(available, 0));
            }
        }

        return null;
    }

    public IReadOnlyList<PendingServiceEntryPart> ToPendingParts() => [.. Selected.Select(draft => draft.ToPending())];

    partial void OnSearchTextChanged(string value) => _ = SearchAsync();

    [RelayCommand]
    private async Task AddAsync(PartSearchResult result)
    {
        if (!result.CanAdd || !HasRoom || Selected.Any(draft => draft.PartId == result.Part.Id))
        {
            return;
        }

        Selected.Add(new ServiceEntryPartDraft(result.Part, result.Available, result.DetailsText));
        OnSelectionChanged();
        await SearchAsync();
    }

    [RelayCommand]
    private async Task RemoveAsync(ServiceEntryPartDraft draft)
    {
        Selected.Remove(draft);
        OnSelectionChanged();
        await SearchAsync();
    }

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelected));
        OnPropertyChanged(nameof(HasRoom));
        OnPropertyChanged(nameof(HasNoResults));
    }

    private PartSearchResult ToResult(Part part)
    {
        var available = PartAvailability.Available(part, _pendingEntries);
        return new PartSearchResult(
            part,
            available,
            $"{part.CatalogNumber} · {formatter.FormatMoney(part.UnitPrice)}",
            available > 0 ? AppText.Format(AppResources.EntryPartAvailable, available) : AppResources.EntryPartOutOfStock,
            available > 0);
    }
}
