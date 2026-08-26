using AirPageSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        if (!await db.Templates.AnyAsync())
        {
            db.Templates.AddRange(
                new PanelTemplate { Name = "最新行情面板", Type = "market", Description = "三大指数、市场广度、涨跌停及异动股票", IsBuiltIn = true },
                new PanelTemplate { Name = "服务端状态面板", Type = "server-status", Description = "CPU、内存、磁盘、网络与进程排行", IsBuiltIn = true },
                new PanelTemplate { Name = "自定义数据面板", Type = "custom", Description = "将HTTP JSON数据绑定到标题、指标和列表", IsBuiltIn = true,
                    SchemaJson = "{"titlePath":"$.title","metrics":[{"label":"状态","path":"$.status"}],"itemsPath":"$.items"}" }
            );
        }
        await db.SaveChangesAsync();
    }
}

