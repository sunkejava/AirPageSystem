using System.Diagnostics;
using System.Text.Json;
using AirPageSystem.Api.Data;
using AirPageSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Services;

public sealed class PanelExecutionService(AppDbContext db, MarketDataProvider market, SystemStatusProvider system,
    ThreeXUiMonitorProvider threeXUi, CustomJsonDataProvider custom, PanelRenderer renderer, AirPageClient airPage, IWebHostEnvironment environment,
    IConfiguration configuration)
{
    public async Task<ExecutionResult> ExecuteAsync(Guid templateId, Guid? deviceId, bool push, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var template = await db.Templates.FindAsync([templateId], ct) ?? throw new KeyNotFoundException("模板不存在。");
        RenderedPanel rendered = template.Type switch
        {
            "market" => renderer.RenderMarket(await market.GetAsync(ct)),
            "server-status" => renderer.RenderSystem(await system.GetAsync(ct)),
            "3xui-monitor" => renderer.RenderThreeXUi(await threeXUi.GetAsync(ct)),
            "custom" => await RenderCustomAsync(template, ct),
            _ => throw new InvalidOperationException($"不支持的模板类型：{template.Type}")
        };
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath,
            configuration["Panel:OutputDirectory"] ?? "data/renders"));
        Directory.CreateDirectory(root);
        var previewName = $"{stamp}-{template.Id:N}.png";
        await File.WriteAllBytesAsync(Path.Combine(root, previewName), rendered.Png, ct);
        PushResult pushed = new(false, false, "仅生成预览");
        AirPageDevice? device = null;
        if (push)
        {
            device = deviceId.HasValue ? await db.Devices.FindAsync([deviceId.Value], ct)
                : await db.Devices.FirstOrDefaultAsync(x => x.IsDefault, ct);
            if (device is null) throw new InvalidOperationException("未配置目标设备或默认设备。");
            pushed = await airPage.PushAsync(device, rendered.Bmp, ct);
        }
        var record = new PushRecord
        {
            TemplateId = template.Id, DeviceId = device?.Id ?? Guid.Empty, UploadSucceeded = pushed.Succeeded,
            Refreshed = pushed.Refreshed, BmpBytes = rendered.Bmp.Length, DurationMs = sw.ElapsedMilliseconds,
            Message = pushed.Message, PreviewPath = $"/api/renders/{previewName}"
        };
        db.PushRecords.Add(record); await db.SaveChangesAsync(ct);
        return new(record.Id, rendered.Bmp, rendered.Png, record.PreviewPath, pushed);
    }

    private async Task<RenderedPanel> RenderCustomAsync(PanelTemplate template, CancellationToken ct)
    {
        if (!template.DataSourceId.HasValue) throw new InvalidOperationException("自定义模板尚未绑定数据源。");
        var source = await db.DataSources.FindAsync([template.DataSourceId.Value], ct)
            ?? throw new InvalidOperationException("绑定的数据源不存在。");
        using var json = await custom.GetAsync(source, ct);
        var schema = JsonSerializer.Deserialize<CustomSchema>(template.SchemaJson ?? "{}") ?? new();
        var title = JsonPath.Read(json.RootElement, schema.TitlePath) ?? template.Name;
        var metrics = schema.Metrics.ToDictionary(x => x.Label, x => JsonPath.Read(json.RootElement, x.Path) ?? "-");
        var rows = new List<IReadOnlyDictionary<string, string>>();
        if (JsonPath.Select(json.RootElement, schema.ItemsPath) is { ValueKind: JsonValueKind.Array } array)
            foreach (var item in array.EnumerateArray().Take(20))
                rows.Add(schema.Columns.ToDictionary(x => x.Label, x => JsonPath.Read(item, x.Path) ?? "-"));
        return renderer.RenderCustom(title, metrics, rows, DateTimeOffset.Now);
    }
}
public sealed record ExecutionResult(Guid RecordId, byte[] Bmp, byte[] Png, string PreviewPath, PushResult Push);
public sealed class CustomSchema
{
    public string TitlePath { get; set; } = "$.title";
    public string ItemsPath { get; set; } = "$.items";
    public List<CustomBinding> Metrics { get; set; } = [];
    public List<CustomBinding> Columns { get; set; } = [];
}
public sealed class CustomBinding { public string Label { get; set; } = ""; public string Path { get; set; } = "$"; }
internal static class JsonPath
{
    public static string? Read(JsonElement root, string path)
    {
        var node = Select(root, path);
        return node?.ValueKind switch
        {
            JsonValueKind.String => node.Value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => node.Value.GetRawText(),
            _ => node?.GetRawText()
        };
    }
    public static JsonElement? Select(JsonElement root, string path)
    {
        var current = root;
        foreach (var segment in path.Trim().TrimStart('$').TrimStart('.').Split('.', StringSplitOptions.RemoveEmptyEntries))
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current)) return null;
        return current;
    }
}
