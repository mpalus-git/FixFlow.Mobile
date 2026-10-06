using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixFlow.Mobile.Core.Features.Parts;

namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public sealed partial class ServiceEntryPartDraft : ObservableObject
{
    public const int MaxQuantity = 1000;

    private readonly Part _part;

    public ServiceEntryPartDraft(Part part, int available, string detailsText)
    {
        ArgumentNullException.ThrowIfNull(part);

        _part = part;
        Available = available;
        DetailsText = detailsText;
        Quantity = 1;
    }

    public Guid PartId => _part.Id;

    public string Name => _part.Name;

    public string DetailsText { get; }

    public int Available { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IncreaseCommand))]
    [NotifyCanExecuteChangedFor(nameof(DecreaseCommand))]
    public partial int Quantity { get; private set; }

    public PendingServiceEntryPart ToPending() => new(_part.Id, _part.Name, _part.CatalogNumber, Quantity, _part.UnitPrice);

    private bool CanIncrease() => Quantity < Math.Min(Available, MaxQuantity);

    private bool CanDecrease() => Quantity > 1;

    [RelayCommand(CanExecute = nameof(CanIncrease))]
    private void Increase() => Quantity = CanIncrease() ? Quantity + 1 : Quantity;

    [RelayCommand(CanExecute = nameof(CanDecrease))]
    private void Decrease() => Quantity = CanDecrease() ? Quantity - 1 : Quantity;
}
