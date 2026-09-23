using Microsoft.Extensions.Options;

namespace Backend.Common;

/// <summary>
/// Single source of "now". Booking dates and slot times are local venue time (Nepal),
/// while timestamps (CreatedAt etc.) are stored in UTC.
/// </summary>
public interface IAppClock
{
    TimeZoneInfo TimeZone { get; }
    DateTime UtcNow { get; }
    DateTime LocalNow { get; }
    DateOnly Today { get; }
    TimeOnly TimeOfDay { get; }

    /// <summary>Converts a local venue date + time to a UTC instant.</summary>
    DateTime ToUtc(DateOnly date, TimeOnly time);

    /// <summary>Converts a UTC instant to local venue time.</summary>
    DateTime ToLocal(DateTime utc);
}

public class AppClock : IAppClock
{
    public AppClock(IOptions<AppOptions> options)
    {
        TimeZone = ResolveTimeZone(options.Value.TimeZone);
    }

    public TimeZoneInfo TimeZone { get; }
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(UtcNow, TimeZone);
    public DateOnly Today => DateOnly.FromDateTime(LocalNow);
    public TimeOnly TimeOfDay => TimeOnly.FromDateTime(LocalNow);

    public DateTime ToUtc(DateOnly date, TimeOnly time)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, TimeZone);
    }

    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);

    private static TimeZoneInfo ResolveTimeZone(string? configured)
    {
        // IANA id works on Linux/macOS and modern Windows; the Windows id is a fallback.
        foreach (var id in new[] { configured, "Asia/Kathmandu", "Nepal Standard Time" })
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }
}