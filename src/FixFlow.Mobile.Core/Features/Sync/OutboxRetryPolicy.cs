namespace FixFlow.Mobile.Core.Features.Sync;

public sealed class OutboxRetryPolicy(Func<double> nextRandom)
{
    private const double JitterRatio = 0.2;

    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
    ];

    public static OutboxRetryPolicy CreateDefault() => new(Random.Shared.NextDouble);

    public TimeSpan NextDelay(int failedAttempts, TimeSpan? retryAfter)
    {
        var baseDelay = Delays[Math.Clamp(failedAttempts, 1, Delays.Length) - 1];
        var delay = baseDelay * (1 - JitterRatio + (nextRandom() * 2 * JitterRatio));
        return retryAfter is { } serverDelay && serverDelay > delay ? serverDelay : delay;
    }
}
