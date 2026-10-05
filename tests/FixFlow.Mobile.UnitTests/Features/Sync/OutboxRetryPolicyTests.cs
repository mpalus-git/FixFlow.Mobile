using FixFlow.Mobile.Core.Features.Sync;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

public sealed class OutboxRetryPolicyTests
{
    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 15)]
    [InlineData(3, 60)]
    [InlineData(4, 300)]
    [InlineData(5, 900)]
    [InlineData(12, 900)]
    public void Should_Back_Off_Up_To_15_Minutes_When_Attempts_Keep_Failing(int failedAttempts, int expectedSeconds)
    {
        new OutboxRetryPolicy(() => 0.5).NextDelay(failedAttempts, null).ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Theory]
    [InlineData(0.0, 4)]
    [InlineData(1.0, 6)]
    public void Should_Spread_Retries_By_20_Percent_When_Jitter_Is_Applied(double random, int expectedSeconds)
    {
        new OutboxRetryPolicy(() => random).NextDelay(1, null).ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void Should_Wait_As_Long_As_Server_Asks_When_Retry_After_Is_Longer()
    {
        new OutboxRetryPolicy(() => 0.5).NextDelay(1, TimeSpan.FromMinutes(2)).ShouldBe(TimeSpan.FromMinutes(2));
    }
}
