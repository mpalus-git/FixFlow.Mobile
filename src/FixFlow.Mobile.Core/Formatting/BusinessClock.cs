namespace FixFlow.Mobile.Core.Formatting;

public sealed class BusinessClock(TimeProvider timeProvider, TimeZoneInfo timeZone)
{
    public const string WarsawTimeZoneId = "Europe/Warsaw";

    public DateOnly Today => DateOnly.FromDateTime(ToBusinessTime(timeProvider.GetUtcNow()).DateTime);

    public static BusinessClock CreateForWarsaw(TimeProvider timeProvider) =>
        new(timeProvider, TimeZoneInfo.FindSystemTimeZoneById(WarsawTimeZoneId));

    public DateTimeOffset ToBusinessTime(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, timeZone);
}
