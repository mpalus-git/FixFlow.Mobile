namespace FixFlow.Mobile.Core.Formatting;

public sealed class BusinessClock(TimeProvider timeProvider, TimeZoneInfo timeZone)
{
    public const string WarsawTimeZoneId = "Europe/Warsaw";

    public DateTimeOffset Now => timeProvider.GetUtcNow();

    public DateOnly Today => ToBusinessDate(Now);

    public static BusinessClock CreateForWarsaw(TimeProvider timeProvider) =>
        new(timeProvider, TimeZoneInfo.FindSystemTimeZoneById(WarsawTimeZoneId));

    public DateTimeOffset ToBusinessTime(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, timeZone);

    public DateTimeOffset FromBusinessTime(DateTime businessDateTime)
    {
        var unspecified = DateTime.SpecifyKind(businessDateTime, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, timeZone.GetUtcOffset(unspecified)).ToUniversalTime();
    }

    public DateOnly ToBusinessDate(DateTimeOffset instant) => DateOnly.FromDateTime(ToBusinessTime(instant).DateTime);
}
