using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

[Collection(GlobalCultureTests.Name)]
public sealed class WorkOrderDetailsPresenterTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 6, 0, 0, TimeSpan.Zero);

    private readonly GlobalCultureScope _cultureScope = new();
    private readonly DisplayFormatter _formatter;
    private readonly WorkOrderDetailsPresenter _presenter;
    private readonly WorkOrder _workOrder = TestWorkOrders.Create("ZL-2026-0042", Now.AddHours(4), WorkOrderStatus.InProgress);

    public WorkOrderDetailsPresenterTests()
    {
        var languageService = new LanguageService(Substitute.For<ISettingsStore>());
        languageService.ChangeLanguage(AppLanguage.Polish);
        _formatter = new DisplayFormatter(languageService, BusinessClock.CreateForWarsaw(new FakeTimeProvider(Now)));
        _presenter = new WorkOrderDetailsPresenter(_formatter);
    }

    [Fact]
    public void Should_Format_Client_Address_And_Device_When_Details_Are_Available()
    {
        var client = new Client(Guid.NewGuid(), "Piekarnia", "Długa", "12A", "31-147", "Kraków", "Anna Kowalska", "+48 600 100 200", null, null);
        var device = new Device(Guid.NewGuid(), client.Id, "SN-1", "Altherma 3", "Daikin", new DateOnly(2021, 3, 14), null);

        var view = _presenter.Present(_workOrder, client, device, [], [], []);

        view.Client.ShouldNotBeNull().AddressText.ShouldBe("Długa 12A, 31-147 Kraków");
        view.Client.ContactText.ShouldBe("Osoba kontaktowa: Anna Kowalska");
        view.Device.ShouldBe(new DeviceView("Daikin Altherma 3", "Nr seryjny: SN-1", "Zainstalowane: 14.03.2021"));
        view.DueText.ShouldBe("Termin: 05.10.2026 12:00");
        view.PartsTotalText.ShouldBeNull();
    }

    [Fact]
    public void Should_Show_Returned_Parts_As_Negative_And_Net_Total_When_Correction_Exists()
    {
        var filter = new ServiceEntryPart(Guid.NewGuid(), "Filtr", "FP-100", 2, 12.34m);
        var work = CreateEntry(isCorrection: false, filter);
        var correction = CreateEntry(isCorrection: true, filter with { Quantity = 1 });

        var view = _presenter.Present(_workOrder, null, null, [work, correction], [], []);

        var returned = view.Entries[1].Parts.ShouldHaveSingleItem();
        returned.QuantityText.ShouldBe("-1 szt.");
        returned.AmountText.ShouldBe(_formatter.FormatMoney(-12.34m));
        view.PartsTotalText.ShouldBe($"Części netto razem: {_formatter.FormatMoney(12.34m)}");
    }

    [Fact]
    public void Should_Show_Work_Time_In_Warsaw_With_Duration_When_Entry_Has_Start_And_Stop()
    {
        var entry = CreateEntry(isCorrection: false) with
        {
            WorkStartedAt = new DateTimeOffset(2026, 10, 5, 6, 15, 0, TimeSpan.Zero),
            WorkFinishedAt = new DateTimeOffset(2026, 10, 5, 8, 45, 0, TimeSpan.Zero),
        };

        var view = _presenter.Present(_workOrder, null, null, [entry], [], []);

        view.Entries.ShouldHaveSingleItem().WorkTimeText.ShouldBe("Czas pracy: 05.10.2026 08:15 – 10:45 (2 h 30 min)");
    }

    [Fact]
    public void Should_Exclude_Current_Work_Order_When_Showing_Device_History()
    {
        var current = new DeviceHistoryItem(_workOrder.Id, _workOrder.DeviceId, _workOrder.Number, WorkOrderStatus.InProgress, WorkOrderPriority.Normal, "Bieżące", "Jan Kowalski", Now, null, null, 0);
        var previous = current with { WorkOrderId = Guid.NewGuid(), Number = "ZL-2026-0007", Status = WorkOrderStatus.Invoiced, CompletedAt = Now.AddDays(-30), ServiceEntryCount = 3 };

        var view = _presenter.Present(_workOrder, null, null, [], [current, previous], []);

        view.History.ShouldBe([new DeviceHistoryView(previous.WorkOrderId, "ZL-2026-0007", "Rozliczone", "05.09.2026 08:00", "Jan Kowalski", "Wpisy: 3")]);
    }

    [Fact]
    public void Should_Append_Queued_Entry_As_Pending_And_Count_Its_Parts_When_Entry_Waits_In_Queue()
    {
        var confirmed = CreateEntry(isCorrection: false, new ServiceEntryPart(Guid.NewGuid(), "Filtr", "FP-100", 1, 10m));
        var pending = new PendingServiceEntry(Guid.CreateVersion7(), "Druga wizyta", false, null, null, null, null, [new PendingServiceEntryPart(Guid.NewGuid(), "Uszczelka", "U-1", 2, 2.5m)]);
        var operation = new OutboxOperation(Guid.CreateVersion7(), _workOrder.Id, OutboxOperationKind.AddServiceEntry, OutboxPayload.Serialize(pending), Now, 0, null, null);

        var view = _presenter.Present(_workOrder, null, null, [confirmed], [], [operation]);

        view.Entries.Select(entry => entry.IsPending).ShouldBe([false, true]);
        view.Entries[1].Note.ShouldBe("Druga wizyta");
        view.PartsTotalText.ShouldBe($"Części netto razem: {_formatter.FormatMoney(15m)}");
    }

    [Theory]
    [InlineData(WorkOrderStatus.Assigned, false)]
    [InlineData(WorkOrderStatus.InProgress, false)]
    [InlineData(WorkOrderStatus.Completed, true)]
    [InlineData(WorkOrderStatus.Invoiced, true)]
    public void Should_Offer_Protocol_Only_When_Work_Order_Is_Completed_Or_Invoiced(WorkOrderStatus status, bool expected)
    {
        _presenter.Present(_workOrder with { Status = status }, null, null, [], [], []).CanDownloadProtocol.ShouldBe(expected);
    }

    public void Dispose() => _cultureScope.Dispose();

    private ServiceEntry CreateEntry(bool isCorrection, params ServiceEntryPart[] parts) =>
        new(Guid.NewGuid(), _workOrder.Id, Guid.NewGuid(), "Jan Kowalski", "Notatka", isCorrection, null, null, null, null, Now, parts);

    [Fact]
    public void Should_Show_Start_Time_And_Elapsed_Work_When_Timer_Is_Running()
    {
        var timer = new WorkTimer(_workOrder.Id, Now.AddMinutes(-75), WorkLocationStatus.Captured, 50.06, 19.94);

        var view = _presenter.PresentWorkTimer(timer, Now, isLocating: false);

        view.ShouldBe(new WorkTimerView("Praca od 05.10.2026 06:45", "Trwa: 1 h 15 min", "Pozycja zapisana", false, false, false));
    }

    [Fact]
    public void Should_Offer_Settings_And_Retry_When_Location_Permission_Was_Denied()
    {
        var timer = new WorkTimer(_workOrder.Id, Now, WorkLocationStatus.Denied, null, null);

        var view = _presenter.PresentWorkTimer(timer, Now, isLocating: false);

        view.LocationText.ShouldBe("Bez pozycji: brak zgody na lokalizację");
        view.CanRetryLocation.ShouldBeTrue();
        view.CanOpenLocationSettings.ShouldBeTrue();
    }

    [Fact]
    public void Should_Show_Locating_Without_Actions_When_Position_Is_Being_Determined()
    {
        var timer = new WorkTimer(_workOrder.Id, Now.AddMinutes(5), WorkLocationStatus.Pending, null, null);

        var view = _presenter.PresentWorkTimer(timer, Now, isLocating: true);

        view.ElapsedText.ShouldBe("Trwa: 0 h 0 min");
        view.LocationText.ShouldBe("Ustalanie pozycji…");
        view.CanRetryLocation.ShouldBeFalse();
    }

    [Fact]
    public void Should_Offer_Retry_When_Position_Was_Left_Pending_By_Interrupted_Start()
    {
        var timer = new WorkTimer(_workOrder.Id, Now, WorkLocationStatus.Pending, null, null);

        var view = _presenter.PresentWorkTimer(timer, Now, isLocating: false);

        view.LocationText.ShouldBe("Bez pozycji: nie udało się ustalić lokalizacji");
        view.CanRetryLocation.ShouldBeTrue();
        view.CanOpenLocationSettings.ShouldBeFalse();
    }
}
