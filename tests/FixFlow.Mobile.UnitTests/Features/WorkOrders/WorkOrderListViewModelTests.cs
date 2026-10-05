using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Screens;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class WorkOrderListViewModelTests
{
    [Fact]
    public async Task Should_Show_Empty_State_When_Refresh_Finds_No_Work_Orders()
    {
        var formatter = new DisplayFormatter(
            new LanguageService(Substitute.For<ISettingsStore>()),
            BusinessClock.CreateForWarsaw(new FakeTimeProvider()));
        var viewModel = new WorkOrderListViewModel(new ScreenState(formatter));

        await viewModel.RefreshCommand.ExecuteAsync(null);

        viewModel.Screen.Status.ShouldBe(ScreenStatus.Empty);
    }
}
