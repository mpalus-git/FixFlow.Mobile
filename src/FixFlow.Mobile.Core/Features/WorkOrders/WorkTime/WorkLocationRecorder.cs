using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Location;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.Core.Features.WorkOrders.WorkTime;

public sealed class WorkLocationRecorder(ILocationService location, IDialogService dialogs, WorkTimerRepository timers)
{
    public static readonly TimeSpan PositionTimeout = TimeSpan.FromSeconds(15);

    public async Task<WorkLocationStatus> RecordAsync(Guid workOrderId, CancellationToken cancellationToken)
    {
        if (!await location.HasPermissionAsync())
        {
            await dialogs.ShowAlertAsync(AppResources.LocationExplanationTitle, AppResources.LocationExplanationMessage, AppResources.ContinueButton);
            if (!await location.RequestPermissionAsync())
            {
                await timers.SetLocationAsync(workOrderId, WorkLocationStatus.Denied, null, null);
                return WorkLocationStatus.Denied;
            }
        }

        if (await location.GetCurrentPositionAsync(PositionTimeout, cancellationToken) is not { } position)
        {
            await timers.SetLocationAsync(workOrderId, WorkLocationStatus.Unavailable, null, null);
            return WorkLocationStatus.Unavailable;
        }

        await timers.SetLocationAsync(workOrderId, WorkLocationStatus.Captured, position.Latitude, position.Longitude);
        return WorkLocationStatus.Captured;
    }
}
