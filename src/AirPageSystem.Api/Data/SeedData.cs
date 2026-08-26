using AirPageSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        var types = await db.Templates.Select(x => x.Type).ToListAsync();
        if (!types.Contains("market")) db.Templates.Add(new PanelTemplate { Name = "最新行情面板", Type = "market", Description = "三大指数、市场广度、涨跌停及异动股票", IsBuiltIn = true });
        if (!types.Contains("server-status")) db.Templates.Add(new PanelTemplate { Name = "服务端状态面板", Type = "server-status", Description = "CPU、内存、磁盘、网络与进程排行", IsBuiltIn = true });
        if (!types.Contains("3xui-monitor")) db.Templates.Add(new PanelTemplate { Name = "3xui代理监控", Type = "3xui-monitor", Description = "3x-ui/Xray状态、流量、入站、客户端、IP与连接数", IsBuiltIn = true });
        if (!types.Contains("custom")) db.Templates.Add(new PanelTemplate { Name = "自定义数据面板", Type = "custom", Description = "将HTTP JSON数据绑定到标题、指标和列表", IsBuiltIn = true,
            SchemaJson = """{"titlePath":"$.title","metrics":[{"label":"状态","path":"$.status"}],"itemsPath":"$.items"}""" });
        await db.SaveChangesAsync();
    }
}
