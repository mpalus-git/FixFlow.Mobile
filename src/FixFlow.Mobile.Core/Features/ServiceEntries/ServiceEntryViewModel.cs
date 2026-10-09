using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixFlow.Mobile.Core.Accessibility;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.Sync.Outbox;
using FixFlow.Mobile.Core.Features.WorkOrders.WorkTime;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.Core.Screens;

namespace FixFlow.Mobile.Core.Features.ServiceEntries;

public sealed partial class ServiceEntryViewModel(
    ScreenState screen,
    ServiceEntryPartsSelection parts,
    PhotoAttacher photoAttacher,
    WorkOrderRepository workOrders,
    ServiceEntryRepository serviceEntries,
    OutboxRepository outbox,
    WorkTimerRepository timers,
    OutboxWriter outboxWriter,
    BusinessClock clock,
    DisplayFormatter formatter,
    INavigationService navigation,
    IDialogService dialogs,
    IScreenReaderAnnouncer announcer) : ObservableObject, INavigatedTo<Guid>, ILeaveGuard
{
    public const int MaxPhotos = 10;

    private Guid _workOrderId;
    private DateTimeOffset? _workOrderStartedAt;
    private DateTimeOffset _initialStart;
    private DateTimeOffset _initialFinish;
    private WorkTimer? _timer;
    private bool _saved;

    public ScreenState Screen { get; } = screen;

    public ServiceEntryPartsSelection Parts { get; } = parts;

    public ObservableCollection<PhotoDraft> Photos { get; } = [];

    public bool HasPhotos => Photos.Count > 0;

    public bool CanAddPhotos => Photos.Count < MaxPhotos;

    public bool CanTakePhoto => CanAddPhotos && photoAttacher.IsCaptureSupported;

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
        var operations = await outbox.GetAllAsync();
        var workOrderOperations = operations.Where(operation => operation.WorkOrderId == parameter).ToList();
        var workOrder = await workOrders.GetAsync(parameter) is { } stored
            ? PendingOperationsOverlay.Apply(stored, workOrderOperations)
            : null;
        if (workOrder is not { Status: WorkOrderStatus.InProgress })
        {
            Screen.ShowError(AppResources.EntryRequiresWorkInProgress);
            return;
        }

        Number = workOrder.Number;
        _workOrderStartedAt = workOrder.StartedAt;
        _timer = await timers.GetAsync(parameter);
        _initialStart = new[] { _timer?.StartedAt, await LastFinishAsync(workOrderOperations) }.Max() ?? workOrder.StartedAt ?? clock.Now;
        _initialFinish = clock.Now;
        (StartDate, StartTime) = ToPickers(_initialStart);
        (FinishDate, FinishTime) = ToPickers(_initialFinish);
        LocationText = _timer?.LocationStatus switch
        {
            WorkLocationStatus.Captured => AppResources.EntryLocationAttached,
            WorkLocationStatus.NotRequired => null,
            _ => AppResources.EntryWithoutLocation,
        };
        await Parts.LoadAsync(PendingOperationsOverlay.PendingEntries(operations));
        Screen.ShowContent(hasItems: true);
    }

    public bool HasUnsavedChanges =>
        !string.IsNullOrWhiteSpace(Note)
        || Parts.Selected.Count > 0
        || Photos.Count > 0
        || Compose(StartDate, StartTime, _initialStart) != _initialStart
        || Compose(FinishDate, FinishTime, _initialFinish) != _initialFinish;

    public async Task<bool> CanLeaveAsync()
    {
        if (_saved || !HasUnsavedChanges)
        {
            return true;
        }

        if (!await ConfirmDiscardAsync())
        {
            return false;
        }

        foreach (var draft in Photos)
        {
            photoAttacher.Discard(draft.Photo);
        }

        Photos.Clear();
        OnPhotosChanged();
        _saved = true;
        return true;
    }

    [RelayCommand]
    private Task ReloadAsync() => OnNavigatedToAsync(_workOrderId);

    [RelayCommand]
    private async Task TakePhotoAsync(CancellationToken cancellationToken)
    {
        if (CanTakePhoto && await photoAttacher.CaptureAsync(cancellationToken) is { } photo)
        {
            AddPhotos([photo]);
        }
    }

    [RelayCommand]
    private async Task PickPhotosAsync(CancellationToken cancellationToken)
    {
        if (CanAddPhotos)
        {
            AddPhotos(await photoAttacher.PickAsync(MaxPhotos - Photos.Count, cancellationToken));
        }
    }

    [RelayCommand]
    private void RemovePhoto(PhotoDraft draft)
    {
        if (Photos.Remove(draft))
        {
            photoAttacher.Discard(draft.Photo);
            OnPhotosChanged();
        }
    }

    private void AddPhotos(IEnumerable<PendingPhoto> photos)
    {
        foreach (var photo in photos)
        {
            Photos.Add(new PhotoDraft(photo, photoAttacher.LocalPathFor(photo)));
        }

        OnPhotosChanged();
    }

    private void OnPhotosChanged()
    {
        OnPropertyChanged(nameof(HasPhotos));
        OnPropertyChanged(nameof(CanAddPhotos));
        OnPropertyChanged(nameof(CanTakePhoto));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var start = ComposeStart();
        var finish = Compose(FinishDate, FinishTime, _initialFinish);
        ValidationMessage = Validate(start, finish) ?? await Parts.ValidateAsync();
        if (ValidationMessage is not null)
        {
            return;
        }

        var hasPosition = _timer is { LocationStatus: WorkLocationStatus.Captured, Latitude: not null, Longitude: not null };
        await outboxWriter.AddServiceEntryAsync(
            _workOrderId,
            new PendingServiceEntry(
                Guid.CreateVersion7(),
                Note.Trim(),
                false,
                start,
                finish,
                hasPosition ? _timer!.Latitude : null,
                hasPosition ? _timer!.Longitude : null,
                Parts.ToPendingParts())
            {
                Photos = [.. Photos.Select(draft => draft.Photo)],
            });
        _saved = true;
        await navigation.GoBackAsync();
    }

    private Task<bool> ConfirmDiscardAsync() => dialogs.ConfirmAsync(
        AppResources.DiscardDraftTitle,
        AppResources.DiscardDraftMessage,
        AppResources.DiscardChangeButton,
        AppResources.KeepEditingButton);

    private async Task<DateTimeOffset?> LastFinishAsync(IReadOnlyList<OutboxOperation> workOrderOperations)
    {
        var stored = await serviceEntries.GetForWorkOrderAsync(_workOrderId);
        return stored.Select(entry => entry.WorkFinishedAt)
            .Concat(PendingOperationsOverlay.PendingEntries(workOrderOperations).Select(entry => entry.WorkFinishedAt))
            .Max();
    }

    private string? Validate(DateTimeOffset start, DateTimeOffset finish)
    {
        if (string.IsNullOrWhiteSpace(Note))
        {
            return AppResources.EntryNoteRequired;
        }

        if (_workOrderStartedAt is { } workOrderStartedAt && start < workOrderStartedAt)
        {
            return AppText.Format(AppResources.EntryStartBeforeWorkOrder, formatter.FormatDateTime(workOrderStartedAt));
        }

        if (finish <= start)
        {
            return AppResources.EntryFinishBeforeStart;
        }

        return finish > clock.Now ? AppResources.EntryFinishInFuture : null;
    }

    private DateTimeOffset ComposeStart()
    {
        var start = Compose(StartDate, StartTime, _initialStart);
        return _workOrderStartedAt is { } workOrderStartedAt && start < workOrderStartedAt && workOrderStartedAt - start < TimeSpan.FromMinutes(1)
            ? workOrderStartedAt
            : start;
    }

    private DateTimeOffset Compose(DateTime? date, TimeSpan? time, DateTimeOffset initial)
    {
        var local = clock.ToBusinessTime(initial);
        var pickedDate = (date ?? local.DateTime).Date;
        var pickedTime = time ?? local.TimeOfDay;
        return pickedDate == local.Date && pickedTime.Hours == local.Hour && pickedTime.Minutes == local.Minute
            ? initial
            : clock.FromBusinessTime(pickedDate + new TimeSpan(pickedTime.Hours, pickedTime.Minutes, 0));
    }

    private (DateTime? Date, TimeSpan? Time) ToPickers(DateTimeOffset instant)
    {
        var local = clock.ToBusinessTime(instant);
        return (local.Date, new TimeSpan(local.Hour, local.Minute, 0));
    }
}
