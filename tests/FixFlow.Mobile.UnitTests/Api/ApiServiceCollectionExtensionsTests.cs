using System.Net;
using FixFlow.Mobile.Core.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace FixFlow.Mobile.UnitTests.Api;

public sealed class ApiServiceCollectionExtensionsTests
{
    private readonly FakeTimeProvider _timeProvider = new();

    [Fact]
    public async Task Should_Not_Retry_Post_When_Server_Returns_503()
    {
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var provider = BuildProvider(handler);
        var api = provider.GetRequiredService<IFixFlowApi>();

        using var response = await AdvanceUntilCompleted(api.StartWorkAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Retry_Get_When_Server_Returns_503()
    {
        var attempts = 0;
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(
            Interlocked.Increment(ref attempts) == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NotFound));
        using var provider = BuildProvider(handler);
        var api = provider.GetRequiredService<IFixFlowApi>();

        using var response = await AdvanceUntilCompleted(api.GetWorkOrderAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        handler.Requests.Count.ShouldBe(2);
        handler.Requests[0].RequestUri!.Authority.ShouldBe("api.test");
    }

    [Fact]
    public void Should_Resolve_Readiness_Probe_When_Api_Is_Registered()
    {
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var provider = BuildProvider(handler);

        provider.GetService<ServerReadinessProbe>().ShouldNotBeNull();
    }

    private ServiceProvider BuildProvider(StubHttpMessageHandler handler)
    {
        var services = new ServiceCollection().AddSingleton<TimeProvider>(_timeProvider);
        services.AddFixFlowApi(new Uri("https://api.test")).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    private async Task<T> AdvanceUntilCompleted<T>(Task<T> call)
    {
        while (!call.IsCompleted)
        {
            _timeProvider.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        return await call;
    }
}
