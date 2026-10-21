namespace hrms.Shared.Services;

// India has no DST, so a fixed +05:30 offset is exact (no tz database needed).
public static class Ist
{
    private static readonly TimeSpan Offset = TimeSpan.FromMinutes(330);

    public static DateTime Of(DateTime utc) =>
        DateTime.SpecifyKind(DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(Offset), DateTimeKind.Unspecified);

    public static DateTime Now => Of(DateTime.UtcNow);
    public static DateOnly Today => DateOnly.FromDateTime(Now);

    // "HH:mm" -> minutes from midnight
    public static int Mins(string hhmm)
    {
        var p = hhmm.Split(':');
        return int.Parse(p[0]) * 60 + int.Parse(p[1]);
    }

    // UTC instant of an IST date + minutes-from-midnight
    public static DateTime ToUtc(DateOnly d, int minutes) =>
        new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc).AddMinutes(minutes) - Offset;
}