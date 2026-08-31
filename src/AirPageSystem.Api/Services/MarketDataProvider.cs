using System.Text.Json;
using System.Text.RegularExpressions;

namespace AirPageSystem.Api.Services;

public sealed class MarketDataProvider(IHttpClientFactory clients)
{
    public async Task<WatchSnapshot> GetStocksAsync(string? schemaJson,CancellationToken ct)
    {
        var codes=WatchCodes(schemaJson);var http=clients.CreateClient();http.DefaultRequestHeaders.UserAgent.ParseAdd("AirPageSystem/1.0");
        var items=new List<WatchItem>();var failures=0;
        foreach(var code in codes)
        {
            try
            {
                var market=code.StartsWith('6')||code.StartsWith('9')?"sh":"sz";var bytes=await http.GetByteArrayAsync($"https://qt.gtimg.cn/q={market}{code}",ct);var text=System.Text.Encoding.GetEncoding("GB18030").GetString(bytes);var quoted=Regex.Match(text,"\\\"(?<v>[^\\\"]+)\\\"");
                if(!quoted.Success){failures++;continue;}var p=quoted.Groups["v"].Value.Split('~');if(p.Length<35){failures++;continue;}var price=Parse(p,3);var previous=Parse(p,4);var high=Parse(p,33);var low=Parse(p,34);var daily=Parse(p,32);var secid=(market=="sh"?"1.":"0.")+code;
                var weekly=await SafeWeeklyStockAsync(http,secid,ct);items.Add(new(code,p[1],price,daily,previous<=0?0:(high-low)/previous*100,weekly));
            }
            catch(Exception) when(!ct.IsCancellationRequested){failures++;}
        }
        return new(ChinaTime.Now,"关注股票行情",items,Status(codes.Length,items.Count,failures));
    }

    public async Task<WatchSnapshot> GetFundsAsync(string? schemaJson,CancellationToken ct)
    {
        var http=clients.CreateClient();http.DefaultRequestHeaders.UserAgent.ParseAdd("AirPageSystem/1.0");var items=new List<WatchItem>();var codes=WatchCodes(schemaJson);var failures=0;
        foreach(var code in codes)
        {
            try
            {
                using var request=new HttpRequestMessage(HttpMethod.Get,$"https://fund.eastmoney.com/pingzhongdata/{code}.js?v={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");request.Headers.Referrer=new Uri($"https://fund.eastmoney.com/{code}.html");using var response=await http.SendAsync(request,ct);response.EnsureSuccessStatusCode();var script=await response.Content.ReadAsStringAsync(ct);
                var name=Regex.Match(script,"var\\s+fS_name\\s*=\\s*\"(?<v>[^\"]+)\"").Groups["v"].Value;var trendMatch=Regex.Match(script,"Data_netWorthTrend\\s*=\\s*(?<v>\\[[^;]+\\])");if(!trendMatch.Success){failures++;continue;}using var trend=JsonDocument.Parse(trendMatch.Groups["v"].Value);var rows=trend.RootElement.EnumerateArray().TakeLast(7).ToArray();if(rows.Length==0){failures++;continue;}var price=Number(rows[^1],"y");var daily=Number(rows[^1],"equityReturn");var first=Number(rows[0],"y");var weekly=first>0?(price/first-1)*100:0;
                items.Add(new(code,string.IsNullOrWhiteSpace(name)?code:name,price,daily,0,weekly));
            }
            catch(Exception) when(!ct.IsCancellationRequested){failures++;}
        }
        return new(ChinaTime.Now,"关注基金行情",items,Status(codes.Length,items.Count,failures));
    }

    private static string[] WatchCodes(string? json){try{using var d=JsonDocument.Parse(json??"{}");return d.RootElement.TryGetProperty("codes",out var c)&&c.ValueKind==JsonValueKind.Array?c.EnumerateArray().Select(x=>x.GetString()?.Trim()).Where(x=>!string.IsNullOrWhiteSpace(x)&&x!.All(char.IsDigit)).Take(10).Cast<string>().ToArray():[];}catch{return[];}}
    private static async Task<double> WeeklyStockAsync(HttpClient http,string secid,CancellationToken ct){using var d=await http.GetFromJsonAsync<JsonDocument>($"https://push2his.eastmoney.com/api/qt/stock/kline/get?secid={secid}&klt=101&fqt=1&lmt=6&fields1=f1&fields2=f51,f52,f53,f54,f55",ct);if(d is null||!d.RootElement.TryGetProperty("data",out var data)||!data.TryGetProperty("klines",out var rows))return 0;var closes=rows.EnumerateArray().Select(x=>double.TryParse(x.GetString()?.Split(',').ElementAtOrDefault(2),out var v)?v:0).Where(x=>x>0).ToArray();return closes.Length>1?(closes[^1]/closes[0]-1)*100:0;}
    private static async Task<double> SafeWeeklyStockAsync(HttpClient http,string secid,CancellationToken ct){try{return await WeeklyStockAsync(http,secid,ct);}catch(Exception) when(!ct.IsCancellationRequested){return 0;}}
    private static double Parse(string[] values,int index)=>index<values.Length&&double.TryParse(values[index],System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out var value)?value:0;
    private static double Number(JsonElement e,string n)=>e.TryGetProperty(n,out var x)&&x.ValueKind==JsonValueKind.Number?x.GetDouble():0;
    private static string String(JsonElement e,string n,string fallback)=>e.TryGetProperty(n,out var x)&&x.ValueKind==JsonValueKind.String?x.GetString()??fallback:fallback;
    private static double DoubleString(JsonElement e,string n)=>double.TryParse(String(e,n,""),out var v)?v:0;
    private static string Status(int requested,int loaded,int failures)=>requested==0?"未配置关注代码":failures==0?$"已加载 {loaded} 项":loaded>0?$"已加载 {loaded}/{requested} 项，部分行情暂不可用":"行情源暂不可用，请稍后刷新";
    public async Task<MarketSnapshot> GetAsync(CancellationToken ct)
    {
        var http = clients.CreateClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AirPageSystem/1.0");
        const string fields = "f12,f14,f2,f3,f7";
        const string filter = "m:0+t:6,m:0+t:80,m:1+t:2,m:1+t:23,m:0+t:81+s:2048";
        using var gate = new SemaphoreSlim(10);
        var pages = await Task.WhenAll(Enumerable.Range(1, 60).Select(async page =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var url = $"https://push2delay.eastmoney.com/api/qt/clist/get?pn={page}&pz=100&po=1&np=1&fltt=2&invt=2&fid=f3&fs={Uri.EscapeDataString(filter)}&fields={fields}";
                using var json = await http.GetFromJsonAsync<JsonDocument>(url, ct);
                var result = new List<StockItem>(100);
                if (json is null || !json.RootElement.TryGetProperty("data", out var data) ||
                    !data.TryGetProperty("diff", out var items)) return result;
                foreach (var x in items.EnumerateArray())
                {
                    if (x.GetProperty("f2").ValueKind != JsonValueKind.Number) continue;
                    result.Add(new(x.GetProperty("f12").GetString()!, x.GetProperty("f14").GetString()!,
                        x.GetProperty("f2").GetDouble(), x.GetProperty("f3").GetDouble(),
                        x.GetProperty("f7").ValueKind == JsonValueKind.Number ? x.GetProperty("f7").GetDouble() : 0));
                }
                return result;
            }
            finally { gate.Release(); }
        }));
        var stocks = pages.SelectMany(x => x).ToList();
        var indices = await GetIndicesAsync(http, ct);
        return new(ChinaTime.Now, indices, stocks.Count(x => x.ChangePercent > 0),
            stocks.Count(x => x.ChangePercent < 0), stocks.Count(x => x.ChangePercent == 0),
            stocks.Count(x => x.ChangePercent >= 9.8), stocks.Count(x => x.ChangePercent <= -9.8),
            stocks.OrderByDescending(x => x.ChangePercent).Take(5).ToArray(),
            stocks.OrderBy(x => x.ChangePercent).Take(5).ToArray(),
            stocks.OrderByDescending(x => x.AmplitudePercent).Take(5).ToArray());
    }

    private static async Task<IReadOnlyList<IndexItem>> GetIndicesAsync(HttpClient http, CancellationToken ct)
    {
        var bytes = await http.GetByteArrayAsync("https://qt.gtimg.cn/q=s_sh000001,s_sz399001,s_sz399006", ct);
        var text = System.Text.Encoding.GetEncoding("GB18030").GetString(bytes);
        var names = new[] { "上证指数", "深证成指", "创业板指" };
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(3).Select((line, i) =>
        {
            var p = line.Split('"')[1].Split('~');
            return new IndexItem(names[i], double.Parse(p[3]), double.Parse(p[5]));
        }).ToArray();
    }
}
public sealed record MarketSnapshot(DateTimeOffset CollectedAt, IReadOnlyList<IndexItem> Indices,
    int Advancers, int Decliners, int Unchanged, int LimitUp, int LimitDown,
    IReadOnlyList<StockItem> Gainers, IReadOnlyList<StockItem> Losers, IReadOnlyList<StockItem> Amplitudes);
public sealed record IndexItem(string Name, double Price, double ChangePercent);
public sealed record StockItem(string Code, string Name, double Price, double ChangePercent, double AmplitudePercent);
public sealed record WatchSnapshot(DateTimeOffset CollectedAt,string Title,IReadOnlyList<WatchItem> Items,string Status);
public sealed record WatchItem(string Code,string Name,double Price,double DailyPercent,double AmplitudePercent,double WeeklyPercent);
