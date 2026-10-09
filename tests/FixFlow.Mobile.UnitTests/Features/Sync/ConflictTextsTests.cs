using System.Net;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

[Collection(GlobalCultureTests.Name)]
public sealed class ConflictTextsTests : IDisposable
{
    private const string ApiDetail = "Detail sent by the API.";
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    private readonly GlobalCultureScope _cultureScope = new();
    private readonly LanguageService _languageService = new(Substitute.For<ISettingsStore>());

    [Theory]
    [InlineData("WorkOrder.NotFound")]
    [InlineData("WorkOrder.NotAssignedToTechnician")]
    [InlineData("WorkOrder.TechnicianAlreadyHasWorkInProgress")]
    [InlineData("WorkOrder.NotInProgress")]
    [InlineData("WorkOrder.NoServiceEntries")]
    [InlineData("WorkOrder.Closed")]
    [InlineData("WorkOrder.InvalidStatusTransition")]
    [InlineData("Part.NotFound")]
    [InlineData("Part.Archived")]
    [InlineData("Part.InsufficientStock")]
    [InlineData("ServiceEntry.IdConflict")]
    [InlineData("ServiceEntry.ReturnExceedsConsumption")]
    [InlineData("Photo.ContentTooLarge")]
    [InlineData("Photo.IdConflict")]
    [InlineData("Photo.DailyLimitExceeded")]
    [InlineData(OutboxDispatcher.MissingPhotoFileCode)]
    public void Should_Translate_Reason_When_Error_Code_Is_Known(string errorCode)
    {
        var reason = ConflictTexts.Reason(CreateConflict(HttpStatusCode.Conflict, errorCode, ApiDetail));

        reason.ShouldNotBe(ApiDetail);
        reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(AppLanguage.Polish, "Brak wystarczającej liczby sztuk części w magazynie.")]
    [InlineData(AppLanguage.English, "There are not enough units of the part in stock.")]
    public void Should_Use_Selected_Language_When_Reason_Is_Translated(AppLanguage language, string expected)
    {
        _languageService.ChangeLanguage(language);

        ConflictTexts.Reason(CreateConflict(HttpStatusCode.Conflict, "Part.InsufficientStock", ApiDetail)).ShouldBe(expected);
    }

    [Fact]
    public void Should_Show_Api_Detail_When_Error_Code_Has_No_Translation()
    {
        var validation = CreateConflict(HttpStatusCode.BadRequest, null, "WorkStartedAt cannot be in the future.");

        ConflictTexts.Reason(validation).ShouldBe("WorkStartedAt cannot be in the future.");
    }

    [Fact]
    public void Should_Show_Status_Code_When_Rejection_Has_No_Details()
    {
        _languageService.ChangeLanguage(AppLanguage.Polish);

        ConflictTexts.Reason(CreateConflict(HttpStatusCode.UnprocessableEntity, null, null)).ShouldBe("Serwer odrzucił zmianę (kod 422).");
    }

    [Fact]
    public void Should_Name_Correction_When_Rejected_Entry_Is_Correction()
    {
        _languageService.ChangeLanguage(AppLanguage.Polish);
        var correction = new PendingServiceEntry(Guid.CreateVersion7(), "Pomyłka", true, null, null, null, null, []);
        var operation = new OutboxOperation(Guid.CreateVersion7(), Guid.NewGuid(), OutboxOperationKind.AddServiceEntry, OutboxPayload.Serialize(correction), Now, 0, null, null);

        ConflictTexts.Kind(operation).ShouldBe("Wpis korygujący");
    }

    public void Dispose() => _cultureScope.Dispose();

    private static SyncConflict CreateConflict(HttpStatusCode status, string? errorCode, string? detail) =>
        new(Guid.CreateVersion7(), Guid.NewGuid(), OutboxOperationKind.AddServiceEntry, "{}", Now, status, errorCode, detail, Now);
}
