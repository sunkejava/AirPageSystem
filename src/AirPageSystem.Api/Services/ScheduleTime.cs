using Cronos;

namespace AirPageSystem.Api.Services;

public static class ScheduleTime
{
    public static (CronExpression Cron, TimeZoneInfo Zone) Parse(string expression, string timeZoneId)
    {
        var cron = CronExpression.Parse(expression, CronFormat.Standard);
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch (TimeZoneNotFoundException) when (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsId))
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
        return (cron, zone);
    }

    public static DateTimeOffset Next(string expression, string timeZoneId, DateTimeOffset fromUtc)
    {
        var (cron, zone) = Parse(expression, timeZoneId);
        return cron.GetNextOccurrence(fromUtc, zone)
            ?? throw new InvalidOperationException("Cron 表达式没有可计算的下次执行时间。");
    }
}
