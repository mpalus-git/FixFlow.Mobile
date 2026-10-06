using CommunityToolkit.Mvvm.ComponentModel;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.Core.Screens;

namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public sealed partial class ServiceEntryViewModel(
    ScreenState screen,
    ServiceEntryPartsSelection parts,
    WorkOrderRepository workOrders,
    OutboxRepository outbox,
    WorkTimerRepository timers,
    BusinessClock clock) : ObservableObject, INavigatedTo<Guid>
{
    public ScreenState Screen { get; } = screen;

    public ServiceEntryPartsSelection Parts { get; } = parts;

    [ObservableProperty]
    public partial string Number { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Note { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTime? StartDate { get; set; }

    [ObservableProperty]
    public partial TimeSpan? StartTime { get; set; }

    [ObservableProperty]
    public partial DateTime? FinishDate { get; set; }

    [ObservableProperty]
    public partial TimeSpan? FinishTime { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLocationText))]
    public partial string? LocationText { get; private set; }

    public bool HasLocationText => LocationText is not null;

    public async Task OnNavigatedToAsync(Guid parameter)
    {
        Screen.StartLoading();
        var operations = await outbox.GetAllAsync();
        var workOrder = await workOrders.GetAsync(parameter) is { } stored
            ? PendingOperationsOverlay.Apply(stored, operations.Where(operation => operation.WorkOrderId == parameter))
            : null;
        if (workOrder is not { Status: WorkOrderStatus.InProgress })
        {
            Screen.ShowError(AppResources.EntryRequiresWorkInProgress);
            return;
        }

        Number = workOrder.Number;
        var timer = await timers.GetAsync(parameter);
        (StartDate, StartTime) = ToPickers(timer?.StartedAt ?? workOrder.StartedAt ?? clock.Now);
        (FinishDate, FinishTime) = ToPickers(clock.Now);
        LocationText = timer?.LocationStatus switch
        {
            WorkLocationStatus.Captured => AppResources.EntryLocationAttached,
            WorkLocationStatus.NotRequired => null,
            _ => AppResources.EntryWithoutLocation,
        };
        await Parts.LoadAsync(PendingOperationsOverlay.PendingEntries(operations));
        Screen.ShowContent(hasItems: true);
    }

    private (DateTime? Date, TimeSpan? Time) ToPickers(DateTimeOffset instant)
    {
        var local = clock.ToBusinessTime(instant);
        return (local.Date, new TimeSpan(local.Hour, local.Minute, 0));
    }
}
