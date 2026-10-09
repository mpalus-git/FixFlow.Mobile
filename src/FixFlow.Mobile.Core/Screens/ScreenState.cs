using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Screens;

public sealed partial class ScreenState(DisplayFormatter formatter) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading), nameof(IsContent), nameof(IsEmpty), nameof(IsError))]
    public partial ScreenStatus Status { get; private set; } = ScreenStatus.Loading;

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    private int _pendingChanges;
    private int _rejectedChanges;

    [ObservableProperty]
    public partial bool IsOffline { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Banner), nameof(HasBanner))]
    public partial string? OfflineMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Banner), nameof(HasBanner))]
    public partial ScreenNotice? Notice { get; private set; }

    public StatusBannerView? Banner => StatusBannerView.Compose(OfflineMessage, Notice, _pendingChanges, _rejectedChanges);

    public bool HasBanner => Banner is not null;

    public bool IsLoading => Status == ScreenStatus.Loading;

    public bool IsContent => Status == ScreenStatus.Content;

    public bool IsEmpty => Status == ScreenStatus.Empty;

    public bool IsError => Status == ScreenStatus.Error;

    public void StartLoading()
    {
        ErrorMessage = null;
        Status = ScreenStatus.Loading;
    }

    public void ShowContent(bool hasItems)
    {
        ErrorMessage = null;
        Status = hasItems ? ScreenStatus.Content : ScreenStatus.Empty;
    }

    public void ShowError(string message)
    {
        ErrorMessage = message;
        Status = ScreenStatus.Error;
    }

    public void SetOffline(DateTimeOffset? lastSyncedAt)
    {
        OfflineMessage = lastSyncedAt is { } syncedAt
            ? string.Format(
                CultureInfo.CurrentCulture,
                CompositeFormat.Parse(AppResources.OfflineLastSync),
                formatter.FormatDateTime(syncedAt))
            : AppResources.OfflineNeverSynced;
        IsOffline = true;
    }

    public void ShowNotice(ScreenNotice notice) => Notice = notice;

    public void ClearNotice() => Notice = null;

    public void SetQueue(int pendingChanges, int rejectedChanges)
    {
        if (pendingChanges == _pendingChanges && rejectedChanges == _rejectedChanges)
        {
            return;
        }

        _pendingChanges = pendingChanges;
        _rejectedChanges = rejectedChanges;
        OnPropertyChanged(nameof(Banner));
        OnPropertyChanged(nameof(HasBanner));
    }

    public void SetOnline()
    {
        IsOffline = false;
        OfflineMessage = null;
    }
}
