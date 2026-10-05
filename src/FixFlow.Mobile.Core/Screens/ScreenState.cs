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

    [ObservableProperty]
    public partial bool IsOffline { get; private set; }

    [ObservableProperty]
    public partial string? OfflineMessage { get; private set; }

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

    public void SetOnline()
    {
        IsOffline = false;
        OfflineMessage = null;
    }
}
