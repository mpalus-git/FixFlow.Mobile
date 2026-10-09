using FixFlow.Mobile.Core.Features.WorkOrders.List;
using FixFlow.Mobile.Core.Navigation;

namespace FixFlow.Mobile.UnitTests.Navigation;

public sealed class NavigationRouteTests
{
    [Fact]
    public void Should_Remove_ViewModel_Suffix_When_Building_Route()
    {
        NavigationRoute.For<WorkOrderListViewModel>().ShouldBe("WorkOrderList");
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(ViewModel))]
    public void Should_Throw_When_Type_Is_Not_A_View_Model(Type type)
    {
        Should.Throw<ArgumentException>(() => NavigationRoute.For(type));
    }

    private sealed class ViewModel;
}
