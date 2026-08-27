using System.Diagnostics;
using System.Text.Json;
using AirPageSystem.Api.Data;
using AirPageSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Services;

public sealed class PanelExecutionService(AppDbContext db, MarketDataProvider market, SystemStatusProvider system,
    ThreeXUiMonitorProvider threeXUi, CustomJsonDataProvider custom, PanelRenderer renderer, AirPageClient airPage, IWebHostEnvironment environment,
    IConfiguration configuration, RetryExecutor retry)
{
    public async Task<ExecutionResult> ExecuteAsync(Guid templateId, Guid? deviceId, bool push, CancellationToken ct, Guid? ownerUserId=null, Guid? retryPolicyId=null)
    {
        var sw = Stopwatch.StartNew();
        var template = await db.Templates.FirstOrDefaultAsync(x=>x.Id==templateId&&(x.OwnerUserId==null||x.OwnerUserId==ownerUserId),ct) ?? throw new KeyNotFoundException("模板不存在或无权访问。");
        var policy=retryPolicyId.HasValue?await db.RetryPolicies.FirstOrDefaultAsync(x=>x.Id==retryPolicyId&&x.OwnerUserId==ownerUserId,ct):null;
        policy??=await db.RetryPolicies.FirstOrDefaultAsync(x=>x.OwnerUserId==ownerUserId&&x.IsDefault,ct);
        policy??=new RetryPolicyDefinition{Name="单次执行",OwnerUserId=ownerUserId??Guid.Empty,MaxAttempts=1,RetryPreview=false,RetryPush=false};
        async Task<RenderedPanel> Build(CancellationToken token)=>template.Type switch
        {
            "market" => renderer.RenderMarket(await market.GetAsync(token)),
            "stock-watch" => renderer.RenderWatch(await market.GetStocksAsync(template.SchemaJson,token),false),
            "fund-watch" => renderer.RenderWatch(await market.GetFundsAsync(template.SchemaJson,token),true),
            "server-status" => renderer.RenderSystem(await system.GetAsync(token)),
            "3xui-monitor" => renderer.RenderThreeXUi(await threeXUi.GetAsync(token)),
            "custom" => await RenderCustomAsync(template,token),
            "designer" => renderer.RenderDesigner(template.Name,template.SchemaJson??"{}",DateTimeOffset.Now),
            "daily-quote" => renderer.RenderDesigner(template.Name,template.SchemaJson??"{}",DateTimeOffset.Now),
            _ => throw new InvalidOperationException($"不支持的模板类型：{template.Type}")
        };
        var renderResult=policy.RetryPreview?await retry.RunAsync(Build,policy,"面板生成",ct):(await Build(ct),1);
        var rendered=renderResult.Item1;var attempts=renderResult.Item2;
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
            device = deviceId.HasValue ? await db.Devices.FirstOrDefaultAsync(x=>x.Id==deviceId.Value&&x.OwnerUserId==ownerUserId,ct)
                : await db.Devices.FirstOrDefaultAsync(x => x.OwnerUserId==ownerUserId&&x.IsDefault, ct);
            if (device is null) throw new InvalidOperationException("未配置目标设备或默认设备。");
            if(policy.RetryPush)try{var pushResult=await retry.RunAsync(async token=>{var value=await airPage.PushAsync(device,rendered.Bmp,token);if(!value.Succeeded)throw new HttpRequestException(value.Message);return value;},policy,"AirPage推送",ct);pushed=pushResult.Value;attempts+=pushResult.Attempts-1;}catch(Exception ex) when(ex is not OperationCanceledException){attempts+=Math.Max(0,policy.MaxAttempts-1);pushed=new(false,false,ex.Message);}
            else pushed=await airPage.PushAsync(device,rendered.Bmp,ct);
        }
        var record = new PushRecord
        {
            TemplateId = template.Id, DeviceId = device?.Id ?? Guid.Empty, UploadSucceeded = pushed.Succeeded,
            Refreshed = pushed.Refreshed, BmpBytes = rendered.Bmp.Length, DurationMs = sw.ElapsedMilliseconds,
            Message = pushed.Message, PreviewPath = $"/api/renders/{previewName}",OwnerUserId=ownerUserId,AttemptCount=attempts
        };
        db.PushRecords.Add(record); await db.SaveChangesAsync(ct);
        return new(record.Id, rendered.Bmp, rendered.Png, record.PreviewPath, pushed);
    }

    public async Task<RenderedPanel> PreviewAsync(string name,string type,Guid? dataSourceId,string? schemaJson,Guid ownerUserId,CancellationToken ct)
    {
        var template=new PanelTemplate{Name=name,Type=type,Description="",DataSourceId=dataSourceId,SchemaJson=schemaJson,OwnerUserId=ownerUserId};
        return type switch
        {
            "market"=>renderer.RenderMarket(await market.GetAsync(ct)),
            "stock-watch"=>renderer.RenderWatch(await market.GetStocksAsync(schemaJson,ct),false),
            "fund-watch"=>renderer.RenderWatch(await market.GetFundsAsync(schemaJson,ct),true),
            "designer"=>renderer.RenderDesigner(name,schemaJson??"{}",DateTimeOffset.Now),
            "daily-quote"=>renderer.RenderDesigner(name,schemaJson??"{}",DateTimeOffset.Now),
            "custom"=>await RenderCustomAsync(template,ct),
            _=>throw new InvalidOperationException("该模板类型不支持代码预览。")
        };
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
