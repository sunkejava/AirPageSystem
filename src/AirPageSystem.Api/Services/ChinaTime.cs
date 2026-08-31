namespace AirPageSystem.Api.Services;

/// <summary>统一提供北京时间，避免部署主机时区影响面板显示。</summary>
public static class ChinaTime
{
    private static readonly TimeZoneInfo Zone = ResolveZone();
    public static DateTimeOffset Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone);
    public static DateTimeOffset Convert(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, Zone);
    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "Asia/Shanghai", "China Standard Time" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { }
        return TimeZoneInfo.CreateCustomTimeZone("CST", TimeSpan.FromHours(8), "北京时间", "北京时间");
    }
}
