using AirPageSystem.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace AirPageSystem.Api.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db, IServiceProvider services, IConfiguration configuration)
    {
        var admin = await db.Users.FirstOrDefaultAsync(x => x.IsAdmin);
        if (admin is null)
        {
            admin = new AppUser { Username = (configuration["BootstrapAdmin:Username"] ?? "admin").Trim().ToLowerInvariant(), DisplayName = "系统管理员", PasswordHash = "", IsAdmin = true, MustChangePassword = true };
            var hasher = services.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<AppUser>>();
            var configuredPassword=configuration["BootstrapAdmin:Password"];var generated=string.IsNullOrWhiteSpace(configuredPassword);var initialPassword=generated?Convert.ToHexString(RandomNumberGenerator.GetBytes(12)):configuredPassword!;
            admin.PasswordHash = hasher.HashPassword(admin,initialPassword);
            db.Users.Add(admin); await db.SaveChangesAsync();
            if(generated)
            {
                var dataDirectory=Path.Combine(AppContext.BaseDirectory,"data");Directory.CreateDirectory(dataDirectory);var passwordFile=Path.Combine(dataDirectory,"bootstrap-admin-password.txt");
                await File.WriteAllTextAsync(passwordFile,$"username={admin.Username}{Environment.NewLine}password={initialPassword}{Environment.NewLine}");
                if(!OperatingSystem.IsWindows())File.SetUnixFileMode(passwordFile,UnixFileMode.UserRead|UnixFileMode.UserWrite);
                services.GetRequiredService<ILoggerFactory>().CreateLogger("BootstrapAdmin").LogWarning("首次启动管理员凭据已写入本机一次性文件 {PasswordFile}。登录并修改密码后请删除该文件。",passwordFile);
            }
        }
        var types = await db.Templates.Select(x => x.Type).ToListAsync();
        if (!types.Contains("market")) db.Templates.Add(new PanelTemplate { Name = "最新行情面板", Type = "market", Description = "三大指数、市场广度、涨跌停及异动股票", IsBuiltIn = true });
        if (!types.Contains("server-status")) db.Templates.Add(new PanelTemplate { Name = "服务端状态面板", Type = "server-status", Description = "CPU、内存、磁盘、网络与进程排行", IsBuiltIn = true });
        if (!types.Contains("3xui-monitor")) db.Templates.Add(new PanelTemplate { Name = "3xui代理监控", Type = "3xui-monitor", Description = "3x-ui/Xray状态、流量、入站、客户端、IP与连接数", IsBuiltIn = true });
        if (!types.Contains("stock-watch")) db.Templates.Add(new PanelTemplate { Name = "关注股票行情", Type = "stock-watch", Description = "自选股票当日涨跌、振幅及近一周表现", IsBuiltIn = true, SchemaJson = "{\"codes\":[\"600519\",\"000001\",\"300750\"]}" });
        if (!types.Contains("fund-watch")) db.Templates.Add(new PanelTemplate { Name = "关注基金行情", Type = "fund-watch", Description = "自选基金最新估值、当日与近一周表现", IsBuiltIn = true, SchemaJson = "{\"codes\":[\"005693\",\"017175\",\"023638\"]}" });
        if (!types.Contains("daily-quote")) db.Templates.Add(new PanelTemplate { Name = "每日金句", Type = "daily-quote", Description = "适合待机展示的高对比度金句卡片", IsBuiltIn = true, SchemaJson = "{\"preset\":\"quote\",\"title\":\"每日金句\",\"quote\":\"保持专注，把复杂的事做简单。\",\"author\":\"佚名\"}" });
        if (!types.Contains("weather-environment")) db.Templates.Add(new PanelTemplate { Name = "天气与环境", Type = "weather-environment", Description = "实时温湿度、风速、降水与未来五日预报", IsBuiltIn = true, SchemaJson = "{\"city\":\"北京\",\"latitude\":39.9042,\"longitude\":116.4074}" });
        if (!types.Contains("news-trending")) db.Templates.Add(new PanelTemplate { Name = "资讯与热榜", Type = "news-trending", Description = "中文热点资讯与来源时间", IsBuiltIn = true });
        if (!types.Contains("bilibili-hot")) db.Templates.Add(new PanelTemplate { Name = "B站热点视频", Type = "bilibili-hot", Description = "B站全站热门视频、UP主与播放量", IsBuiltIn = true });
        if (!types.Contains("ai-news")) db.Templates.Add(new PanelTemplate { Name = "最新AI新闻", Type = "ai-news", Description = "人工智能领域最新中文资讯", IsBuiltIn = true });
        if (!types.Contains("custom")) db.Templates.Add(new PanelTemplate { Name = "自定义数据面板", Type = "custom", Description = "将HTTP JSON数据绑定到标题、指标和列表", IsBuiltIn = true,
            SchemaJson = """{"titlePath":"$.title","metrics":[{"label":"状态","path":"$.status"}],"itemsPath":"$.items"}""" });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Devices SET OwnerUserId={admin.Id} WHERE OwnerUserId IS NULL");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE DataSources SET OwnerUserId={admin.Id} WHERE OwnerUserId IS NULL");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Schedules SET OwnerUserId={admin.Id} WHERE OwnerUserId IS NULL");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE PushRecords SET OwnerUserId={admin.Id} WHERE OwnerUserId IS NULL");
        if (!await db.RetryPolicies.AnyAsync(x => x.OwnerUserId == admin.Id))
            db.RetryPolicies.Add(new RetryPolicyDefinition { OwnerUserId=admin.Id, Name="默认重试", MaxAttempts=3, InitialDelayMs=500, BackoffFactor=2, MaxDelayMs=5000, IsDefault=true });
        await db.SaveChangesAsync();
    }
}
