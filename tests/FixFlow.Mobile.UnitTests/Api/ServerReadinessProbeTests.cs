using System.Net;
using FixFlow.Mobile.Core.Api;
using Microsoft.Extensions.Time.Testing;

namespace FixFlow.Mobile.UnitTests.Api;

public sealed class ServerReadinessProbeTests
{
    private readonly FakeTimeProvider _timeProvider = new();
    private int _startingNotices;

    [Fact]
    public async Task Should_Return_Ready_Without_Starting_Notice_When_Server_Responds_At_Once()
    {
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var ready = await AdvanceUntilCompleted(StartProbe(handler));

        ready.ShouldBeTrue();
        _startingNotices.ShouldBe(0);
        handler.Requests.ShouldHaveSingleItem().RequestUri.ShouldBe(new Uri("https://api.test/api/v1/system/ready"));
    }

    [Fact]
    public async Task Should_Notify_Starting_When_Server_Does_Not_Respond_Within_3_Seconds()
    {
        var pendingResponse = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHttpMessageHandler((_, cancellationToken) => pendingResponse.Task.WaitAsync(cancellationToken));
        var probe = StartProbe(handler);

        await AdvanceBy(TimeSpan.FromSeconds(2));
        _startingNotices.ShouldBe(0);
        await AdvanceBy(TimeSpan.FromSeconds(1));
        _startingNotices.ShouldBe(1);

        pendingResponse.SetResult(new HttpResponseMessage(HttpStatusCode.OK));

        (await AdvanceUntilCompleted(probe)).ShouldBeTrue();
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Retry_Every_3_Seconds_When_Server_Returns_503()
    {
        var attempts = 0;
        using var handler = new StubHttpMessageHandler(_ => Interlocked.Increment(ref attempts) switch
        {
            1 => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            2 => throw new HttpRequestException("Connection refused"),
            _ => new HttpResponseMessage(HttpStatusCode.OK),
        });
        var startedAt = _timeProvider.GetUtcNow();

        var ready = await AdvanceUntilCompleted(StartProbe(handler));

        ready.ShouldBeTrue();
        handler.Requests.Count.ShouldBe(3);
        _startingNotices.ShouldBe(1);
        (_timeProvider.GetUtcNow() - startedAt).ShouldBeInRange(TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(7));
    }

    [Fact]
    public async Task Should_Give_Up_After_90_Seconds_When_Server_Stays_Unavailable()
    {
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var startedAt = _timeProvider.GetUtcNow();

        var ready = await AdvanceUntilCompleted(StartProbe(handler), stepSeconds: 3);

        ready.ShouldBeFalse();
        _startingNotices.ShouldBe(1);
        (_timeProvider.GetUtcNow() - startedAt).ShouldBeLessThanOrEqualTo(ServerReadinessProbe.MaxWaitTime);
        handler.Requests.Count.ShouldBe(30);
    }

    [Fact]
    public async Task Should_Give_Up_When_Server_Holds_Requests_Until_Deadline()
    {
        using var handler = new StubHttpMessageHandler((_, cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ContinueWith(
                _ => new HttpResponseMessage(HttpStatusCode.OK),
                TaskScheduler.Default));
        var startedAt = _timeProvider.GetUtcNow();

        var ready = await AdvanceUntilCompleted(StartProbe(handler), stepSeconds: 3);

        ready.ShouldBeFalse();
        (_timeProvider.GetUtcNow() - startedAt).ShouldBeLessThanOrEqualTo(ServerReadinessProbe.MaxWaitTime + TimeSpan.FromSeconds(1));
    }

    private Task<bool> StartProbe(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };
        var probe = new ServerReadinessProbe(httpClient, _timeProvider);

        return probe.WaitUntilReadyAsync(() => _startingNotices++, TestContext.Current.CancellationToken);
    }

    private async Task AdvanceBy(TimeSpan duration)
    {
        await Task.Delay(10, TestContext.Current.CancellationToken);
        _timeProvider.Advance(duration);
        await Task.Delay(10, TestContext.Current.CancellationToken);
    }

    private async Task<T> AdvanceUntilCompleted<T>(Task<T> task, int stepSeconds = 1)
    {
        while (!task.IsCompleted)
        {
            await AdvanceBy(TimeSpan.FromSeconds(stepSeconds));
        }

        return await task;
    }
}
