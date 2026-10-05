using FixFlow.Mobile.Core.Api;

namespace FixFlow.Mobile.IntegrationTests;

public sealed class ServerReadinessIntegrationTests(ApiEnvironment environment)
{
    [Fact]
    public async Task Should_Report_Ready_Without_Starting_Notice_When_Api_Container_Is_Running()
    {
        using var httpClient = new HttpClient { BaseAddress = environment.BaseAddress };
        var startingNotices = 0;

        var ready = await new ServerReadinessProbe(httpClient, TimeProvider.System)
            .WaitUntilReadyAsync(() => startingNotices++, TestContext.Current.CancellationToken);

        ready.ShouldBeTrue();
        startingNotices.ShouldBe(0);
    }
}
