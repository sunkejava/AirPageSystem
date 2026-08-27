using System.Text.Json;

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
                var secid=(code.StartsWith('6')||code.StartsWith('9')?"1.":"0.")+code;
                using var quote=await http.GetFromJsonAsync<JsonDocument>($"https://push2.eastmoney.com/api/qt/stock/get?secid={secid}&fields=f57,f58,f43,f44,f45,f60,f170",ct);
                if(quote is null||!quote.RootElement.TryGetProperty("data",out var d)||d.ValueKind!=JsonValueKind.Object){failures++;continue;}
                var price=Number(d,"f43")/100d;var high=Number(d,"f44")/100d;var low=Number(d,"f45")/100d;var previous=Number(d,"f60")/100d;
                var weekly=await WeeklyStockAsync(http,secid,ct);items.Add(new(code,String(d,"f58",code),price,Number(d,"f170")/100d,previous<=0?0:(high-low)/previous*100,weekly));
            }
            catch(Exception) when(!ct.IsCancellationRequested){failures++;}
        }
        return new(DateTimeOffset.Now,"关注股票行情",items,Status(codes.Length,items.Count,failures));
    }

    public async Task<WatchSnapshot> GetFundsAsync(string? schemaJson,CancellationToken ct)
    {
        var http=clients.CreateClient();http.DefaultRequestHeaders.UserAgent.ParseAdd("AirPageSystem/1.0");var items=new List<WatchItem>();var codes=WatchCodes(schemaJson);var failures=0;
        foreach(var code in codes)
        {
            try
            {
                using var valuation=new HttpRequestMessage(HttpMethod.Get,$"https://fundgz.1234567.com.cn/js/{code}.js");valuation.Headers.Referrer=new Uri("https://fund.eastmoney.com/");
                using var valuationResponse=await http.SendAsync(valuation,ct);valuationResponse.EnsureSuccessStatusCode();var text=await valuationResponse.Content.ReadAsStringAsync(ct);var start=text.IndexOf('{');var end=text.LastIndexOf('}');if(start<0||end<=start){failures++;continue;}
                using var doc=JsonDocument.Parse(text[start..(end+1)]);var d=doc.RootElement;var price=DoubleString(d,"gsz");var daily=DoubleString(d,"gszzl");
                using var request=new HttpRequestMessage(HttpMethod.Get,$"https://api.fund.eastmoney.com/f10/lsjz?fundCode={code}&pageIndex=1&pageSize=7");request.Headers.Referrer=new Uri("https://fundf10.eastmoney.com/");
                using var response=await http.SendAsync(request,ct);var weekly=0d;if(response.IsSuccessStatusCode){using var history=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));if(history.RootElement.TryGetProperty("Data",out var data)&&data.TryGetProperty("LSJZList",out var rows)){var navs=rows.EnumerateArray().Select(x=>double.TryParse(String(x,"DWJZ",""),out var v)?v:0).Where(x=>x>0).ToArray();if(navs.Length>1)weekly=(navs[0]/navs[^1]-1)*100;}}
                items.Add(new(code,String(d,"name",code),price,daily,0,weekly));
            }
            catch(Exception) when(!ct.IsCancellationRequested){failures++;}
        }
        return new(DateTimeOffset.Now,"关注基金行情",items,Status(codes.Length,items.Count,failures));
    }

    private static string[] WatchCodes(string? json){try{using var d=JsonDocument.Parse(json??"{}");return d.RootElement.TryGetProperty("codes",out var c)&&c.ValueKind==JsonValueKind.Array?c.EnumerateArray().Select(x=>x.GetString()?.Trim()).Where(x=>!string.IsNullOrWhiteSpace(x)&&x!.All(char.IsDigit)).Take(10).Cast<string>().ToArray():[];}catch{return[];}}
    private static async Task<double> WeeklyStockAsync(HttpClient http,string secid,CancellationToken ct){using var d=await http.GetFromJsonAsync<JsonDocument>($"https://push2his.eastmoney.com/api/qt/stock/kline/get?secid={secid}&klt=101&fqt=1&lmt=6&fields1=f1&fields2=f51,f52,f53,f54,f55",ct);if(d is null||!d.RootElement.TryGetProperty("data",out var data)||!data.TryGetProperty("klines",out var rows))return 0;var closes=rows.EnumerateArray().Select(x=>double.TryParse(x.GetString()?.Split(',').ElementAtOrDefault(2),out var v)?v:0).Where(x=>x>0).ToArray();return closes.Length>1?(closes[^1]/closes[0]-1)*100:0;}
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
        return new(DateTimeOffset.Now, indices, stocks.Count(x => x.ChangePercent > 0),
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
