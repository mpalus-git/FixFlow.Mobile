using System.Net;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Formatting;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

[Collection(GlobalCultureTests.Name)]
public sealed class SyncPresenterTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkOrderId = Guid.NewGuid();
    private static readonly Guid OtherWorkOrderId = Guid.NewGuid();
    private static readonly Dictionary<Guid, string> Numbers = new() { [WorkOrderId] = "ZL-2026-0042", [OtherWorkOrderId] = "ZL-2026-0043" };

    private readonly GlobalCultureScope _cultureScope = new();
    private readonly SyncPresenter _presenter;

    public SyncPresenterTests()
    {
        var languageService = new LanguageService(Substitute.For<ISettingsStore>());
        languageService.ChangeLanguage(AppLanguage.Polish);
        var formatter = new DisplayFormatter(languageService, BusinessClock.CreateForWarsaw(new FakeTimeProvider(Now)));
        _presenter = new SyncPresenter(new LocalChangeDescriber(formatter), formatter);
    }

    [Theory]
    [InlineData(0, 0, "Synchronizacja zakończona.")]
    [InlineData(2, 0, "Synchronizacja zakończona. Do wysłania: 2.")]
    [InlineData(1, 3, "Synchronizacja zakończona. Do wysłania: 1. Do wyjaśnienia: 3.")]
    public void Should_Describe_Remaining_Changes_When_Manual_Sync_Completes(int pending, int rejected, string expected) =>
        SyncPresenter.DescribeCompletion(pending, rejected).ShouldBe(expected);

    [Fact]
    public void Should_Show_Reason_Rejection_Time_And_Held_Changes_When_Conflict_Is_Presented()
    {
        var start = Create(WorkOrderId, OutboxOperationKind.StartWork, OutboxPayload.Serialize(new PendingStart(Now)));
        var conflict = new SyncConflict(
            start.Id, WorkOrderId, start.Kind, start.Payload, Now, HttpStatusCode.Conflict, "WorkOrder.TechnicianAlreadyHasWorkInProgress", null, Now.AddMinutes(5));
        var complete = Create(WorkOrderId, OutboxOperationKind.CompleteWorkOrder, OutboxPayload.Empty);

        var view = _presenter.PresentConflicts([conflict], [complete], Numbers).ShouldHaveSingleItem();

        view.Title.ShouldBe("ZL-2026-0042 · Rozpoczęcie pracy");
        view.Reason.ShouldBe("Masz już inne zlecenie w toku. Zakończ je, zanim rozpoczniesz kolejne.");
        view.Details.ShouldBe("Rozpoczęto: 05.10.2026 10:00");
        view.RejectedAtText.ShouldBe("Odrzucono 05.10.2026 10:05");
        view.Content.ShouldBe($"ZL-2026-0042 · Rozpoczęcie pracy{Environment.NewLine}Rozpoczęto: 05.10.2026 10:00");
        view.FollowingChangeCount.ShouldBe(1);
        view.FollowingChangesText.ShouldBe("Kolejne zmiany tego zlecenia wstrzymane do wyjaśnienia: 1");
    }

    [Fact]
    public void Should_Mark_Only_Operations_Of_Conflicting_Work_Order_As_Held_When_Queue_Is_Presented()
    {
        var conflict = new SyncConflict(
            Guid.CreateVersion7(), WorkOrderId, OutboxOperationKind.StartWork, OutboxPayload.Empty, Now, HttpStatusCode.NotFound, "WorkOrder.NotFound", null, Now);
        var held = Create(WorkOrderId, OutboxOperationKind.CompleteWorkOrder, OutboxPayload.Empty);
        var waiting = Create(OtherWorkOrderId, OutboxOperationKind.CompleteWorkOrder, OutboxPayload.Empty);

        var views = _presenter.PresentPending([held, waiting], [conflict], Numbers);

        views.Select(view => (view.Title, view.StatusText, view.IsHeld)).ShouldBe(
        [
            ("ZL-2026-0042 · Zakończenie zlecenia", "Wstrzymane do wyjaśnienia konfliktu", true),
            ("ZL-2026-0043 · Zakończenie zlecenia", "Oczekuje na wysłanie", false),
        ]);
    }

    [Fact]
    public void Should_Show_Failed_Attempts_And_Next_Attempt_When_Operation_Waits_For_Retry()
    {
        var retried = Create(WorkOrderId, OutboxOperationKind.CompleteWorkOrder, OutboxPayload.Empty) with
        {
            AttemptCount = 2,
            NextAttemptAt = Now.AddMinutes(1),
        };

        var view = _presenter.PresentPending([retried], [], Numbers).ShouldHaveSingleItem();

        view.StatusText.ShouldBe("Nieudane próby: 2. Następna próba: 05.10.2026 10:01");
    }

    public void Dispose() => _cultureScope.Dispose();

    private static OutboxOperation Create(Guid workOrderId, OutboxOperationKind kind, string payload) =>
        new(Guid.CreateVersion7(), workOrderId, kind, payload, Now, 0, null, null);
}
