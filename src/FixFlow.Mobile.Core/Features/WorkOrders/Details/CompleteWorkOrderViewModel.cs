using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixFlow.Mobile.Core.Accessibility;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync.Outbox;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Photos;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.Core.Screens;

namespace FixFlow.Mobile.Core.Features.WorkOrders.Details;

public sealed partial class CompleteWorkOrderViewModel(
    ScreenState screen,
    WorkOrderRepository workOrders,
    ServiceEntryRepository serviceEntries,
    OutboxRepository outbox,
    OutboxWriter outboxWriter,
    PhotoStore photos,
    INavigationService navigation,
    IDialogService dialogs,
    IScreenReaderAnnouncer announcer) : ObservableObject, INavigatedTo<Guid>, ILeaveGuard
{
    private Guid _workOrderId;
    private bool _saved;

    public ScreenState Screen { get; } = screen;

    [ObservableProperty]
    public partial string Message { get; private set; } = string.Empty;

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
        ValidationMessage = null;
        Screen.StartLoading();
        var operations = (await outbox.GetAllAsync()).Where(operation => operation.WorkOrderId == parameter).ToList();
        var workOrder = await workOrders.GetAsync(parameter) is { } stored ? PendingOperationsOverlay.Apply(stored, operations) : null;
        if (workOrder is not { Status: WorkOrderStatus.InProgress })
        {
            Screen.ShowError(AppResources.EntryRequiresWorkInProgress);
            return;
        }

        if ((await serviceEntries.GetForWorkOrderAsync(parameter)).Count == 0 && PendingOperationsOverlay.PendingEntries(operations).Count == 0)
        {
            Screen.ShowError(AppResources.CompleteRequiresEntry);
            return;
        }

        Message = AppText.Format(AppResources.CompleteConfirmMessage, workOrder.Number);
        Screen.ShowContent(hasItems: true);
    }

    public ISignatureSource? Signature { get; set; }

    public async Task<bool> CanLeaveAsync() =>
        _saved
        || Signature is not { IsEmpty: false }
        || await dialogs.ConfirmAsync(
            AppResources.DiscardDraftTitle,
            AppResources.DiscardDraftMessage,
            AppResources.DiscardChangeButton,
            AppResources.KeepEditingButton);

    [RelayCommand]
    private Task ReloadAsync() => OnNavigatedToAsync(_workOrderId);

    [RelayCommand]
    private async Task CompleteAsync(ISignatureSource? signature, CancellationToken cancellationToken)
    {
        Guid? signaturePhotoId = null;
        if (signature is { IsEmpty: false })
        {
            var jpeg = await signature.ExportJpegAsync(cancellationToken);
            if (!PhotoCompression.IsJpeg(jpeg) || jpeg.Length > PhotoCompression.MaxBytes)
            {
                ValidationMessage = AppResources.SignatureInvalid;
                return;
            }

            signaturePhotoId = Guid.CreateVersion7();
            await photos.SaveAsync(signaturePhotoId.Value, jpeg);
        }

        await outboxWriter.CompleteWorkOrderAsync(_workOrderId, signaturePhotoId);
        _saved = true;
        await navigation.GoBackAsync();
    }
}
