using System.Globalization;
using FixFlow.Mobile.Core.Formatting;
using Microsoft.Extensions.Time.Testing;

namespace FixFlow.Mobile.UnitTests.Formatting;

public sealed class BusinessClockTests
{
    private readonly FakeTimeProvider _timeProvider = new();

    public BusinessClockTests()
    {
        _timeProvider.SetLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
    }

    [Fact]
    public void Should_Return_Warsaw_Date_When_Utc_Date_Is_Still_Previous_Day()
    {
        _timeProvider.SetUtcNow(new DateTimeOffset(2026, 10, 4, 22, 30, 0, TimeSpan.Zero));

        BusinessClock.CreateForWarsaw(_timeProvider).Today.ShouldBe(new DateOnly(2026, 10, 5));
    }

    [Theory]
    [InlineData("2026-07-01T10:00:00Z", "2026-07-01T12:00:00+02:00")]
    [InlineData("2026-01-15T10:00:00Z", "2026-01-15T11:00:00+01:00")]
    [InlineData("2026-10-25T00:30:00Z", "2026-10-25T02:30:00+02:00")]
    [InlineData("2026-10-25T01:30:00Z", "2026-10-25T02:30:00+01:00")]
    public void Should_Convert_To_Warsaw_Offset_When_Instant_Is_In_Summer_Or_Winter_Time(string utc, string expected)
    {
        var clock = BusinessClock.CreateForWarsaw(_timeProvider);

        var businessTime = clock.ToBusinessTime(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture));

        businessTime.ToString("yyyy-MM-ddTHH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture).ShouldBe(expected);
    }

    [Theory]
    [InlineData("2026-07-01T12:00:00", "2026-07-01T10:00:00Z")]
    [InlineData("2026-01-15T11:00:00", "2026-01-15T10:00:00Z")]
    public void Should_Convert_Warsaw_Wall_Clock_To_Utc_When_Device_Is_In_Other_Zone(string warsaw, string expectedUtc)
    {
        var clock = BusinessClock.CreateForWarsaw(_timeProvider);

        clock.FromBusinessTime(DateTime.Parse(warsaw, CultureInfo.InvariantCulture)).ShouldBe(DateTimeOffset.Parse(expectedUtc, CultureInfo.InvariantCulture));
    }
}
