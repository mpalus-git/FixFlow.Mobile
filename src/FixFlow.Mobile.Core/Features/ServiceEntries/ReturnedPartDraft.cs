using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public sealed partial class ReturnedPartDraft(NetPartUsage usage, string detailsText) : ObservableObject
{
    public NetPartUsage Usage { get; } = usage;

    public string DetailsText { get; } = detailsText;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IncreaseCommand))]
    [NotifyCanExecuteChangedFor(nameof(DecreaseCommand))]
    public partial int Quantity { get; private set; }

    public PendingServiceEntryPart ToPending() =>
        new(Usage.PartId, Usage.PartName, Usage.CatalogNumber, Quantity, Usage.LastUnitPrice);

    private bool CanIncrease() => Quantity < Usage.NetQuantity;

    private bool CanDecrease() => Quantity > 0;

    [RelayCommand(CanExecute = nameof(CanIncrease))]
    private void Increase() => Quantity = CanIncrease() ? Quantity + 1 : Quantity;

    [RelayCommand(CanExecute = nameof(CanDecrease))]
    private void Decrease() => Quantity = CanDecrease() ? Quantity - 1 : Quantity;
}
