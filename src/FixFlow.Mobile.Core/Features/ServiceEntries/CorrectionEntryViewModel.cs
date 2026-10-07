using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixFlow.Mobile.Core.Accessibility;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.Core.Screens;

namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public sealed partial class CorrectionEntryViewModel(
    ScreenState screen,
    WorkOrderRepository workOrders,
    ServiceEntryRepository serviceEntries,
    OutboxRepository outbox,
    OutboxWriter outboxWriter,
    INavigationService navigation,
    IScreenReaderAnnouncer announcer) : ObservableObject, INavigatedTo<Guid>
{
    private Guid _workOrderId;

    public ScreenState Screen { get; } = screen;

    public ObservableCollection<ReturnedPartDraft> Parts { get; } = [];

    [ObservableProperty]
    public partial string Number { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Reason { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationMessage))]
    public partial string? ValidationMessage { get; private set; }

    public bool HasValidationMessage => ValidationMessage is not null;

    partial void OnValidationMessageChanged(string? value)
    {
        if (value is not null)
        {
            announcer.Announce(value);
        }
    }

    public async Task OnNavigatedToAsync(Guid parameter)
    {
        _workOrderId = parameter;
        Screen.StartLoading();
        Parts.Clear();
        var operations = (await outbox.GetAllAsync()).Where(operation => operation.WorkOrderId == parameter).ToList();
        var workOrder = await workOrders.GetAsync(parameter) is { } stored ? PendingOperationsOverlay.Apply(stored, operations) : null;
        if (workOrder is not { Status: WorkOrderStatus.InProgress })
        {
            Screen.ShowError(AppResources.EntryRequiresWorkInProgress);
            return;
        }

        Number = workOrder.Number;
        var usage = NetPartUsage.Calculate(await serviceEntries.GetForWorkOrderAsync(parameter), PendingOperationsOverlay.PendingEntries(operations));
        if (usage.Count == 0)
        {
            Screen.ShowError(AppResources.CorrectionNothingToReturn);
            return;
        }

        foreach (var part in usage)
        {
            Parts.Add(new ReturnedPartDraft(part, WorkOrderTexts.Format(AppResources.CorrectionNetUsed, part.CatalogNumber, part.NetQuantity)));
        }

        Screen.ShowContent(hasItems: true);
    }

    [RelayCommand]
    private Task ReloadAsync() => OnNavigatedToAsync(_workOrderId);

    [RelayCommand]
    private async Task SaveAsync()
    {
        var returned = Parts.Where(part => part.Quantity > 0).Select(part => part.ToPending()).ToList();
        ValidationMessage = string.IsNullOrWhiteSpace(Reason) ? AppResources.CorrectionReasonRequired
            : returned.Count == 0 ? AppResources.CorrectionNoPartsSelected
            : null;
        if (ValidationMessage is not null)
        {
            return;
        }

        await outboxWriter.AddServiceEntryAsync(
            _workOrderId,
            new PendingServiceEntry(Guid.CreateVersion7(), Reason.Trim(), true, null, null, null, null, returned));
        await navigation.GoBackAsync();
    }
}
