namespace FixFlow.Mobile.Core.Api;

public sealed class ServerReadinessProbe(HttpClient httpClient, TimeProvider timeProvider)
{
    public const string ReadinessPath = "api/v1/system/ready";

    public static readonly TimeSpan SlowResponseThreshold = TimeSpan.FromSeconds(3);

    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(3);

    public static readonly TimeSpan MaxWaitTime = TimeSpan.FromSeconds(90);

    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(45);

    public async Task<bool> WaitUntilReadyAsync(Action onStarting, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onStarting);

        var deadline = timeProvider.GetUtcNow() + MaxWaitTime;
        var startingReported = false;

        void ReportStarting()
        {
            if (!startingReported)
            {
                startingReported = true;
                onStarting();
            }
        }

        while (true)
        {
            var remaining = deadline - timeProvider.GetUtcNow();
            var attemptTimeout = remaining < AttemptTimeout ? remaining : AttemptTimeout;

            if (await IsReadyAsync(attemptTimeout, ReportStarting, cancellationToken))
            {
                return true;
            }

            ReportStarting();

            if (deadline - timeProvider.GetUtcNow() <= RetryInterval)
            {
                return false;
            }

            await Task.Delay(RetryInterval, timeProvider, cancellationToken);
        }
    }

    private async Task<bool> IsReadyAsync(TimeSpan timeout, Action reportStarting, CancellationToken cancellationToken)
    {
        using var timeoutSource = new CancellationTokenSource(timeout, timeProvider);
        using var requestSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        using var slowResponseSource = CancellationTokenSource.CreateLinkedTokenSource(requestSource.Token);

        var request = httpClient.GetAsync(new Uri(ReadinessPath, UriKind.Relative), requestSource.Token);
        var slowResponse = Task.Delay(SlowResponseThreshold, timeProvider, slowResponseSource.Token);

        if (await Task.WhenAny(request, slowResponse) == slowResponse && slowResponse.IsCompletedSuccessfully)
        {
            reportStarting();
        }

        await slowResponseSource.CancelAsync();

        try
        {
            using var response = await request;
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
