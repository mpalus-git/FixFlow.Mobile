using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.Sync.Outbox;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Location;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class WorkLocationRecorderTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkOrderId = Guid.NewGuid();

    private readonly TestUserDatabase _userDatabase = new();
    private readonly ILocationService _location = Substitute.For<ILocationService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly WorkTimerRepository _timers;
    private readonly WorkLocationRecorder _recorder;

    public WorkLocationRecorderTests()
    {
        _timers = new WorkTimerRepository(_userDatabase.Database);
        _recorder = new WorkLocationRecorder(_location, _dialogs, _timers);
    }

    public async ValueTask InitializeAsync()
    {
        await _userDatabase.Database.OpenAsync(Guid.NewGuid());
        await new OutboxWriter(new OutboxRepository(_userDatabase.Database), new FakeTimeProvider(Now), new WeakReferenceMessenger()).StartWorkAsync(WorkOrderId);
    }

    [Fact]
    public async Task Should_Store_Position_Without_Asking_When_Permission_Is_Granted()
    {
        _location.HasPermissionAsync().Returns(true);
        _location.GetCurrentPositionAsync(WorkLocationRecorder.PositionTimeout, Arg.Any<CancellationToken>()).Returns(new GeoPosition(50.06, 19.94));

        var status = await _recorder.RecordAsync(WorkOrderId, CancellationToken.None);

        status.ShouldBe(WorkLocationStatus.Captured);
        (await _timers.GetAsync(WorkOrderId)).ShouldBe(new WorkTimer(WorkOrderId, Now, WorkLocationStatus.Captured, 50.06, 19.94));
        await _location.DidNotReceive().RequestPermissionAsync();
        await _dialogs.DidNotReceiveWithAnyArgs().ShowAlertAsync(default!, default!, default!);
    }

    [Fact]
    public async Task Should_Explain_Before_Asking_For_Permission_When_It_Is_Missing()
    {
        _location.RequestPermissionAsync().Returns(true);
        _location.GetCurrentPositionAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(new GeoPosition(50.06, 19.94));

        await _recorder.RecordAsync(WorkOrderId, CancellationToken.None);

        Received.InOrder(() =>
        {
            _dialogs.ShowAlertAsync(AppResources.LocationExplanationTitle, AppResources.LocationExplanationMessage, AppResources.ContinueButton);
            _location.RequestPermissionAsync();
        });
    }

    [Fact]
    public async Task Should_Keep_Work_Without_Position_When_Permission_Is_Denied()
    {
        var status = await _recorder.RecordAsync(WorkOrderId, CancellationToken.None);

        status.ShouldBe(WorkLocationStatus.Denied);
        (await _timers.GetAsync(WorkOrderId)).ShouldNotBeNull().LocationStatus.ShouldBe(WorkLocationStatus.Denied);
        await _location.DidNotReceiveWithAnyArgs().GetCurrentPositionAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_Mark_Position_Unavailable_When_Location_Times_Out()
    {
        _location.HasPermissionAsync().Returns(true);
        _location.GetCurrentPositionAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns((GeoPosition?)null);

        var status = await _recorder.RecordAsync(WorkOrderId, CancellationToken.None);

        status.ShouldBe(WorkLocationStatus.Unavailable);
        (await _timers.GetAsync(WorkOrderId)).ShouldBe(new WorkTimer(WorkOrderId, Now, WorkLocationStatus.Unavailable, null, null));
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();
}
