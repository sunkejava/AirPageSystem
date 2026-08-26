using System.Text.Json;

namespace AirPageSystem.Api.Services;

public sealed class MarketDataProvider(IHttpClientFactory clients)
{
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
