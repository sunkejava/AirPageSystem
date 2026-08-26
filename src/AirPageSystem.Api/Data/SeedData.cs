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
            if(generated)services.GetRequiredService<ILoggerFactory>().CreateLogger("BootstrapAdmin").LogWarning("首次启动管理员 {Username} 的一次性密码：{Password}。登录后请立即修改。",admin.Username,initialPassword);
        }
        var types = await db.Templates.Select(x => x.Type).ToListAsync();
        if (!types.Contains("market")) db.Templates.Add(new PanelTemplate { Name = "最新行情面板", Type = "market", Description = "三大指数、市场广度、涨跌停及异动股票", IsBuiltIn = true });
        if (!types.Contains("server-status")) db.Templates.Add(new PanelTemplate { Name = "服务端状态面板", Type = "server-status", Description = "CPU、内存、磁盘、网络与进程排行", IsBuiltIn = true });
        if (!types.Contains("3xui-monitor")) db.Templates.Add(new PanelTemplate { Name = "3xui代理监控", Type = "3xui-monitor", Description = "3x-ui/Xray状态、流量、入站、客户端、IP与连接数", IsBuiltIn = true });
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
