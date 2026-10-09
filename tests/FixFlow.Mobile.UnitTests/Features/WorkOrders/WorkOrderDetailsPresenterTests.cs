using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.Auth;
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
    private readonly SessionStore _session = new(Substitute.For<ISecureStore>(), new FakeTimeProvider(Now));
    private readonly WorkOrderDetailsPresenter _presenter;
    private readonly WorkOrder _workOrder = TestWorkOrders.Create("ZL-2026-0042", Now.AddHours(4), WorkOrderStatus.InProgress);

    public WorkOrderDetailsPresenterTests()
    {
        var languageService = new LanguageService(Substitute.For<ISettingsStore>());
        languageService.ChangeLanguage(AppLanguage.Polish);
        _formatter = new DisplayFormatter(languageService, BusinessClock.CreateForWarsaw(new FakeTimeProvider(Now)));
        _presenter = new WorkOrderDetailsPresenter(_formatter, _session, new ApiAddress(new Uri("https://api.test/")));
    }

    [Fact]
    public void Should_Format_Client_Address_And_Device_When_Details_Are_Available()
    {
        var workOrder = _workOrder with { ClientContact = new ClientContact("Długa", "12A", "31-147", "Kraków", "Anna Kowalska", "+48 600 100 200") };
        var device = new Device(Guid.NewGuid(), workOrder.ClientId, "SN-1", "Altherma 3", "Daikin", new DateOnly(2021, 3, 14), null);

        var view = _presenter.Present(workOrder, device, [], [], [], []);

        view.Client.ShouldNotBeNull().AddressText.ShouldBe("Długa 12A, 31-147 Kraków");
        view.Client.ContactText.ShouldBe("Osoba kontaktowa: Anna Kowalska");
        view.Device.ShouldBe(new DeviceView("Daikin Altherma 3", "Nr seryjny: SN-1", "Zainstalowane: 14.03.2021"));
        view.DueText.ShouldBe("Termin: 05.10.2026 12:00");
        view.PartsTotalText.ShouldBeNull();
    }

    [Fact]
    public void Should_Hide_Client_When_Contact_Was_Not_Stored_Yet()
    {
        _presenter.Present(_workOrder, null, [], [], [], []).Client.ShouldBeNull();
    }

    [Fact]
    public void Should_Describe_History_With_Technician_Due_Date_Change_And_System_Actor()
    {
        var events = new[]
        {
            Event(WorkOrderEventType.Created, null, null, Now),
            Event(WorkOrderEventType.Assigned, "Anna Nowak", "Jan Kowalski", Now),
            Event(WorkOrderEventType.Updated, "Anna Nowak", "Jan Kowalski", Now.AddDays(1)),
            Event(WorkOrderEventType.Updated, "Anna Nowak", "Jan Kowalski", Now.AddDays(1)),
            Event(WorkOrderEventType.Reassigned, "Anna Nowak", "Piotr Zieliński", Now.AddDays(1)),
            Event(WorkOrderEventType.Unassigned, "Anna Nowak", null, Now.AddDays(1)),
            Event(WorkOrderEventType.Started, "Jan Kowalski", "Jan Kowalski", Now.AddDays(1)),
            Event(WorkOrderEventType.Completed, "Jan Kowalski", "Jan Kowalski", Now.AddDays(1)),
            Event(WorkOrderEventType.Invoiced, "Anna Nowak", "Jan Kowalski", Now.AddDays(1)),
        };

        var view = _presenter.Present(_workOrder, null, [], [], events, []);

        view.Events.Select(item => item.Title).ShouldBe([
            "Utworzono zlecenie",
            "Przypisano: Jan Kowalski",
            "Zmieniono termin: 06.10.2026 08:00",
            "Zaktualizowano zlecenie",
            "Przekazano: Piotr Zieliński",
            "Cofnięto przypisanie",
            "Rozpoczęto pracę",
            "Zakończono zlecenie",
            "Rozliczono zlecenie",
        ]);
        view.Events[0].ShouldBe(new WorkOrderEventView("Utworzono zlecenie", "05.10.2026 08:00", "System", false));
        view.Events[1].ActorText.ShouldBe("Anna Nowak");
    }

    [Fact]
    public void Should_Show_Local_Signature_Until_Sent_And_Server_Address_Afterwards()
    {
        var signatureId = Guid.NewGuid();
        var signed = _workOrder with { Status = WorkOrderStatus.Completed, ClientSignaturePhotoId = signatureId };

        var pending = _presenter.Present(signed, null, [], [], [], [], id => id == signatureId ? "/data/photos/signature.jpg" : null);
        var sent = _presenter.Present(signed, null, [], [], [], [], _ => null);

        pending.ClientSignature.ShouldBe(new EntryPhotoView("/data/photos/signature.jpg", true));
        sent.ClientSignature.ShouldBe(new EntryPhotoView($"https://api.test/api/v1/photos/{signatureId}", false));
        _presenter.Present(_workOrder, null, [], [], [], []).HasClientSignature.ShouldBeFalse();
    }

    [Fact]
    public void Should_Append_Queued_Start_And_Completion_As_Pending_Events()
    {
        var start = new OutboxOperation(Guid.CreateVersion7(), _workOrder.Id, OutboxOperationKind.StartWork, OutboxPayload.Serialize(new PendingStart(Now)), Now, 0, null, null);
        var complete = new OutboxOperation(Guid.CreateVersion7(), _workOrder.Id, OutboxOperationKind.CompleteWorkOrder, OutboxPayload.Serialize(new PendingCompletion(Now.AddHours(2))), Now, 0, null, null);

        var view = _presenter.Present(_workOrder, null, [], [], [Event(WorkOrderEventType.Assigned, "Anna Nowak", "Jan Kowalski", Now)], [start, complete]);

        view.Events.Select(item => (item.Title, item.DateText, item.IsPending)).ShouldBe([
            ("Przypisano: Jan Kowalski", "05.10.2026 08:00", false),
            ("Rozpoczęto pracę", "05.10.2026 08:00", true),
            ("Zakończono zlecenie", "05.10.2026 10:00", true),
        ]);
    }

    [Fact]
    public void Should_Show_Returned_Parts_As_Negative_And_Net_Total_When_Correction_Exists()
    {
        var filter = new ServiceEntryPart(Guid.NewGuid(), "Filtr", "FP-100", 2, 12.34m);
        var work = CreateEntry(isCorrection: false, filter);
        var correction = CreateEntry(isCorrection: true, filter with { Quantity = 1 });

        var view = _presenter.Present(_workOrder, null, [work, correction], [], [], []);

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

        var view = _presenter.Present(_workOrder, null, [entry], [], [], []);

        view.Entries.ShouldHaveSingleItem().WorkTimeText.ShouldBe("Czas pracy: 05.10.2026 08:15 – 10:45 (2\u00A0h\u00A030\u00A0min)");
    }

    [Theory]
    [InlineData("2026-10-05T21:30:00Z", "2026-10-05T22:30:00Z", "Czas pracy: 05.10.2026 23:30 – 06.10.2026 00:30 (1\u00A0h\u00A00\u00A0min)")]
    [InlineData("2026-10-07T10:40:00Z", "2026-10-09T10:49:00Z", "Czas pracy: 07.10.2026 12:40 – 09.10.2026 12:49 (48\u00A0h\u00A09\u00A0min)")]
    public void Should_Show_End_Date_When_Work_Ends_On_Another_Day_In_Warsaw(string startedAt, string finishedAt, string expected)
    {
        var entry = CreateEntry(isCorrection: false) with
        {
            WorkStartedAt = DateTimeOffset.Parse(startedAt, System.Globalization.CultureInfo.InvariantCulture),
            WorkFinishedAt = DateTimeOffset.Parse(finishedAt, System.Globalization.CultureInfo.InvariantCulture),
        };

        var view = _presenter.Present(_workOrder, null, [entry], [], [], []);

        view.Entries.ShouldHaveSingleItem().WorkTimeText.ShouldBe(expected);
    }

    [Fact]
    public void Should_Exclude_Current_Work_Order_When_Showing_Device_History()
    {
        var current = new DeviceHistoryItem(_workOrder.Id, _workOrder.DeviceId, _workOrder.Number, WorkOrderStatus.InProgress, WorkOrderPriority.Normal, "Bieżące", "Jan Kowalski", Now, null, null, 0);
        var previous = current with { WorkOrderId = Guid.NewGuid(), Number = "ZL-2026-0007", Status = WorkOrderStatus.Invoiced, CompletedAt = Now.AddDays(-30), ServiceEntryCount = 3 };

        var view = _presenter.Present(_workOrder, null, [], [current, previous], [], []);

        view.History.ShouldBe([new DeviceHistoryView(previous.WorkOrderId, "ZL-2026-0007", "Rozliczone", "05.09.2026 08:00", "Jan Kowalski", "Wpisy: 3")]);
    }

    [Fact]
    public void Should_Append_Queued_Entry_As_Pending_And_Count_Its_Parts_When_Entry_Waits_In_Queue()
    {
        var confirmed = CreateEntry(isCorrection: false, new ServiceEntryPart(Guid.NewGuid(), "Filtr", "FP-100", 1, 10m));
        var pending = new PendingServiceEntry(Guid.CreateVersion7(), "Druga wizyta", false, null, null, null, null, [new PendingServiceEntryPart(Guid.NewGuid(), "Uszczelka", "U-1", 2, 2.5m)]);
        var operation = new OutboxOperation(Guid.CreateVersion7(), _workOrder.Id, OutboxOperationKind.AddServiceEntry, OutboxPayload.Serialize(pending), Now, 0, null, null);

        var view = _presenter.Present(_workOrder, null, [confirmed], [], [], [operation]);

        view.Entries.Select(entry => entry.IsPending).ShouldBe([false, true]);
        view.Entries[1].Note.ShouldBe("Druga wizyta");
        view.PartsTotalText.ShouldBe($"Części netto razem: {_formatter.FormatMoney(15m)}");
    }

    [Fact]
    public async Task Should_Show_Current_Technician_As_Author_When_Entry_Is_Pending()
    {
        await _session.StartAsync(
            new AuthTokensResponse("access", Now.AddMinutes(15), "refresh", Now.AddDays(7)),
            new SessionUser(Guid.NewGuid(), "jan@fixflow.test", "Jan Kowalski", SessionUser.TechnicianRole));
        var pending = new PendingServiceEntry(Guid.CreateVersion7(), "Offline", false, null, null, null, null, []);
        var operation = new OutboxOperation(Guid.CreateVersion7(), _workOrder.Id, OutboxOperationKind.AddServiceEntry, OutboxPayload.Serialize(pending), Now, 0, null, null);

        var view = _presenter.Present(_workOrder, null, [], [], [], [operation]);

        view.Entries.ShouldHaveSingleItem().TechnicianName.ShouldBe("Jan Kowalski");
    }

    [Theory]
    [InlineData(WorkOrderStatus.Assigned, false)]
    [InlineData(WorkOrderStatus.InProgress, false)]
    [InlineData(WorkOrderStatus.Completed, true)]
    [InlineData(WorkOrderStatus.Invoiced, true)]
    public void Should_Offer_Protocol_Only_When_Work_Order_Is_Completed_Or_Invoiced(WorkOrderStatus status, bool expected)
    {
        _presenter.Present(_workOrder with { Status = status }, null, [], [], [], []).CanDownloadProtocol.ShouldBe(expected);
    }

    [Fact]
    public void Should_Hide_Protocol_When_Completion_Waits_In_Queue()
    {
        var complete = new OutboxOperation(Guid.CreateVersion7(), _workOrder.Id, OutboxOperationKind.CompleteWorkOrder, OutboxPayload.Serialize(new PendingCompletion(Now)), Now, 0, null, null);

        var view = _presenter.Present(_workOrder with { Status = WorkOrderStatus.Completed }, null, [], [], [], [complete]);

        view.CanDownloadProtocol.ShouldBeFalse();
    }

    private WorkOrderEvent Event(WorkOrderEventType type, string? actorName, string? technicianName, DateTimeOffset dueDate) =>
        new(Guid.NewGuid(), _workOrder.Id, type, Now, actorName, technicianName, dueDate);

    public void Dispose() => _cultureScope.Dispose();

    private ServiceEntry CreateEntry(bool isCorrection, params ServiceEntryPart[] parts) =>
        new(Guid.NewGuid(), _workOrder.Id, Guid.NewGuid(), "Jan Kowalski", "Notatka", isCorrection, null, null, null, null, Now, parts);

    [Fact]
    public void Should_Show_Start_Time_And_Elapsed_Work_When_Timer_Is_Running()
    {
        var timer = new WorkTimer(_workOrder.Id, Now.AddMinutes(-75), WorkLocationStatus.Captured, 50.06, 19.94);

        var view = _presenter.PresentWorkTimer(timer, Now, isLocating: false);

        view.ShouldBe(new WorkTimerView("Praca od 05.10.2026 06:45", "Trwa: 1\u00A0h\u00A015\u00A0min", "Pozycja zapisana", false, false, false));
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

        view.ElapsedText.ShouldBe("Trwa: 0\u00A0h\u00A00\u00A0min");
        view.LocationText.ShouldBe("Ustalanie pozycji…");
        view.CanRetryLocation.ShouldBeFalse();
    }

    [Fact]
    public void Should_Hide_Location_When_Position_Was_Already_Attached_To_Entry()
    {
        var timer = new WorkTimer(_workOrder.Id, Now, WorkLocationStatus.NotRequired, null, null);

        var view = _presenter.PresentWorkTimer(timer, Now, isLocating: false);

        view.HasLocationText.ShouldBeFalse();
        view.CanRetryLocation.ShouldBeFalse();
        view.CanOpenLocationSettings.ShouldBeFalse();
    }

    [Fact]
    public void Should_Show_Local_File_Until_Upload_And_Address_Afterwards_When_Entry_Has_Photos()
    {
        var waiting = new PendingPhoto(Guid.NewGuid(), "https://api.test/api/v1/photos/waiting");
        var sent = new PendingPhoto(Guid.NewGuid(), "https://api.test/api/v1/photos/sent");
        var pending = new PendingServiceEntry(Guid.CreateVersion7(), "Zdjęcia", false, Now.AddHours(-1), Now, null, null, []) { Photos = [waiting, sent] };
        var operation = new OutboxOperation(Guid.CreateVersion7(), _workOrder.Id, OutboxOperationKind.AddServiceEntry, OutboxPayload.Serialize(pending), Now, 0, null, null);
        var stored = new ServiceEntry(Guid.NewGuid(), _workOrder.Id, Guid.NewGuid(), "Jan", "Z serwera", false, null, null, null, null, Now, [])
        {
            PhotoUrls = ["https://upload.wikimedia.org/demo.jpg"],
        };

        var view = _presenter.Present(_workOrder, null, [stored], [], [], [operation], id => id == waiting.Id ? "/data/photos/waiting.jpg" : null);

        view.Entries[0].Photos.ShouldBe([new EntryPhotoView("https://upload.wikimedia.org/demo.jpg", false)]);
        view.Entries[1].Photos.ShouldBe([new EntryPhotoView("/data/photos/waiting.jpg", true), new EntryPhotoView(sent.Url, false)]);
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
