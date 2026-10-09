using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Formatting;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

[Collection(GlobalCultureTests.Name)]
public sealed class LocalChangeDescriberTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkOrderId = Guid.NewGuid();

    private readonly GlobalCultureScope _cultureScope = new();
    private readonly LocalChangeDescriber _describer;

    public LocalChangeDescriberTests()
    {
        var languageService = new LanguageService(Substitute.For<ISettingsStore>());
        languageService.ChangeLanguage(AppLanguage.Polish);
        _describer = new LocalChangeDescriber(new DisplayFormatter(languageService, BusinessClock.CreateForWarsaw(new FakeTimeProvider(Now))));
    }

    [Fact]
    public void Should_Describe_Work_Time_Note_Parts_And_Photos_When_Change_Is_Service_Entry()
    {
        var entry = new PendingServiceEntry(
            Guid.CreateVersion7(), "Wymiana filtra", false, Now, Now.AddMinutes(90), 50.06, 19.94,
            [new PendingServiceEntryPart(Guid.NewGuid(), "Filtr powietrza", "FP-100", 2, 12.34m)])
        {
            Photos = [new PendingPhoto(Guid.CreateVersion7(), "https://api.test/api/v1/photos/1")],
        };

        var text = _describer.Describe(Create(OutboxOperationKind.AddServiceEntry, OutboxPayload.Serialize(entry)), "ZL-2026-0042");

        text.Split(Environment.NewLine).ShouldBe(
        [
            "ZL-2026-0042 · Wpis serwisowy",
            "Czas pracy: 05.10.2026 10:00 – 05.10.2026 11:30",
            "Notatka: Wymiana filtra",
            "Zużyte części:",
            "- Filtr powietrza (FP-100): 2 szt.",
            "Zdjęcia: 1",
        ]);
    }

    [Fact]
    public void Should_Describe_Reason_And_Returned_Parts_When_Change_Is_Correction()
    {
        var correction = new PendingServiceEntry(
            Guid.CreateVersion7(), "Zła część", true, null, null, null, null,
            [new PendingServiceEntryPart(Guid.NewGuid(), "Filtr powietrza", "FP-100", 1, 12.34m)]);

        var text = _describer.Describe(Create(OutboxOperationKind.AddServiceEntry, OutboxPayload.Serialize(correction)), "ZL-2026-0042");

        text.Split(Environment.NewLine).ShouldBe(
        [
            "ZL-2026-0042 · Wpis korygujący",
            "Powód korekty: Zła część",
            "Części do zwrotu:",
            "- Filtr powietrza (FP-100): 1 szt.",
        ]);
    }

    [Fact]
    public void Should_Describe_Start_Time_Without_Number_When_Work_Order_Is_Not_Stored_Locally()
    {
        var text = _describer.Describe(Create(OutboxOperationKind.StartWork, OutboxPayload.Serialize(new PendingStart(Now))), null);

        text.Split(Environment.NewLine).ShouldBe(["Rozpoczęcie pracy", "Rozpoczęto: 05.10.2026 10:00"]);
    }

    [Fact]
    public void Should_Describe_Completion_Time_When_Change_Is_Completion()
    {
        var text = _describer.Describe(Create(OutboxOperationKind.CompleteWorkOrder, OutboxPayload.Serialize(new PendingCompletion(Now))), "ZL-2026-0042");

        text.Split(Environment.NewLine).ShouldBe(["ZL-2026-0042 · Zakończenie zlecenia", "Zakończono: 05.10.2026 10:00"]);
    }

    public void Dispose() => _cultureScope.Dispose();

    private static OutboxOperation Create(OutboxOperationKind kind, string payload) =>
        new(Guid.CreateVersion7(), WorkOrderId, kind, payload, Now, 0, null, null);
}
