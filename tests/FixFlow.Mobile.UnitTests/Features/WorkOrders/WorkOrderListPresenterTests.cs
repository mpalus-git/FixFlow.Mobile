using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Resources;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

[Collection(GlobalCultureTests.Name)]
public sealed class WorkOrderListPresenterTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 6, 0, 0, TimeSpan.Zero);

    private readonly GlobalCultureScope _cultureScope = new();
    private readonly LanguageService _languageService = new(Substitute.For<ISettingsStore>());
    private readonly WorkOrderListPresenter _presenter;

    public WorkOrderListPresenterTests()
    {
        var clock = BusinessClock.CreateForWarsaw(new FakeTimeProvider(Now));
        _presenter = new WorkOrderListPresenter(new WorkOrderSectionClassifier(clock), new DisplayFormatter(_languageService, clock));
        _languageService.ChangeLanguage(AppLanguage.Polish);
    }

    [Fact]
    public void Should_Show_Polish_Section_Status_And_Warsaw_Due_Date_When_Work_Order_Is_Due_Today()
    {
        var workOrder = TestWorkOrders.Create("ZL-2026-0042", new DateTimeOffset(2026, 10, 5, 12, 30, 0, TimeSpan.Zero), WorkOrderStatus.InProgress) with
        {
            Priority = WorkOrderPriority.Critical,
        };

        var group = _presenter.Present([workOrder], new HashSet<Guid>(), new HashSet<Guid>()).ShouldHaveSingleItem();

        group.Title.ShouldBe("Dziś");
        var item = group.ShouldHaveSingleItem();
        item.DateText.ShouldBe("Termin: 05.10.2026 14:30");
        item.StatusText.ShouldBe("W toku");
        item.PriorityText.ShouldBe("Krytyczny priorytet");
        item.IsInProgress.ShouldBeTrue();
        item.IsUrgent.ShouldBeTrue();
    }

    [Fact]
    public void Should_Show_Completion_Time_When_Work_Order_Was_Completed_Recently()
    {
        var workOrder = TestWorkOrders.Create("ZL-2026-0007", Now.AddDays(-3), WorkOrderStatus.Completed, Now.AddDays(-1)) with
        {
            Priority = WorkOrderPriority.High,
        };

        var item = _presenter.Present([workOrder], new HashSet<Guid>(), new HashSet<Guid>()).ShouldHaveSingleItem().ShouldHaveSingleItem();

        item.DateText.ShouldBe("Zakończono: 04.10.2026 08:00");
        item.IsUrgent.ShouldBeFalse();
    }

    [Fact]
    public void Should_Mark_Work_Order_When_Its_Changes_Wait_In_Queue()
    {
        var workOrder = TestWorkOrders.Create("ZL-2026-0042", Now.AddHours(2));

        var item = _presenter.Present([workOrder], new HashSet<Guid> { workOrder.Id }, new HashSet<Guid>()).ShouldHaveSingleItem().ShouldHaveSingleItem();

        item.IsPending.ShouldBeTrue();
    }

    [Fact]
    public void Should_Describe_Card_And_Section_For_Screen_Reader_In_Reading_Order_When_Changes_Wait_In_Queue()
    {
        var workOrder = TestWorkOrders.Create("ZL-2026-0042", Now.AddHours(2));

        var group = _presenter.Present([workOrder], new HashSet<Guid> { workOrder.Id }, new HashSet<Guid>()).ShouldHaveSingleItem();
        var item = group.ShouldHaveSingleItem();

        group.ToString().ShouldBe("Dziś");
        item.ToString().ShouldBe($"ZL-2026-0042, {item.StatusText}, {item.ClientName}, {item.DeviceModel}, {item.DateText}, {AppResources.PendingSyncLabel}");
    }

    [Fact]
    public void Should_Mark_Rejected_Instead_Of_Pending_When_Work_Order_Has_Conflict()
    {
        var workOrder = TestWorkOrders.Create("ZL-2026-0042", Now.AddHours(2));
        var ids = new HashSet<Guid> { workOrder.Id };

        var item = _presenter.Present([workOrder], ids, ids).ShouldHaveSingleItem().ShouldHaveSingleItem();

        item.HasRejectedChange.ShouldBeTrue();
        item.IsPending.ShouldBeFalse();
    }

    [Theory]
    [InlineData(AppLanguage.Polish)]
    [InlineData(AppLanguage.English)]
    public void Should_Have_Text_For_Every_Section_Status_And_Priority_When_Language_Is_Selected(AppLanguage language)
    {
        _languageService.ChangeLanguage(language);

        var keys = Enum.GetNames<WorkOrderSection>().Select(name => $"WorkOrderSection{name}")
            .Concat(Enum.GetNames<WorkOrderStatus>().Select(name => $"WorkOrderStatus{name}"))
            .Concat(Enum.GetNames<WorkOrderPriority>().Select(name => $"WorkOrderPriority{name}"));

        keys.ShouldAllBe(key => !string.IsNullOrWhiteSpace(AppResources.ResourceManager.GetString(key, AppResources.Culture)));
    }

    public void Dispose() => _cultureScope.Dispose();
}
