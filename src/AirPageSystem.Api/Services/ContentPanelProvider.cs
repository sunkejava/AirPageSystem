using System.Text.Json;
using System.Xml.Linq;

namespace AirPageSystem.Api.Services;

public sealed class ContentPanelProvider(IHttpClientFactory clients, ILogger<ContentPanelProvider> logger)
{
    public async Task<ContentSnapshot> GetWeatherAsync(string? schemaJson, CancellationToken ct)
    {
        var (city,lat,lon)=WeatherSettings(schemaJson);var rows=new List<ContentRow>();var metrics=new Dictionary<string,string>();
        try
        {
            var http=Client();var url=$"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max&timezone=Asia%2FShanghai&forecast_days=5";
            using var doc=await http.GetFromJsonAsync<JsonDocument>(url,ct);var root=doc!.RootElement;var current=root.GetProperty("current");
            metrics["城市"]=city;metrics["温度"]=$"{Num(current,"temperature_2m"):0.#}℃";metrics["体感"]=$"{Num(current,"apparent_temperature"):0.#}℃";metrics["湿度"]=$"{Num(current,"relative_humidity_2m"):0}%";metrics["风速"]=$"{Num(current,"wind_speed_10m"):0.#} km/h";metrics["降水"]=$"{Num(current,"precipitation"):0.#} mm";
            var daily=root.GetProperty("daily");var dates=daily.GetProperty("time").EnumerateArray().ToArray();var max=daily.GetProperty("temperature_2m_max").EnumerateArray().ToArray();var min=daily.GetProperty("temperature_2m_min").EnumerateArray().ToArray();var rain=daily.GetProperty("precipitation_probability_max").EnumerateArray().ToArray();
            for(var i=0;i<dates.Length;i++)rows.Add(new(dates[i].GetString()??"-",$"{min[i].GetDouble():0}~{max[i].GetDouble():0}℃",$"降水 {rain[i].GetDouble():0}%"));
            return new("天气与环境",DateTimeOffset.Now,metrics,rows,"实时天气与未来5日预报","Open-Meteo");
        }
        catch(Exception ex) when(!ct.IsCancellationRequested){logger.LogWarning(ex,"Weather panel source unavailable.");return Unavailable("天气与环境","天气数据暂不可用","Open-Meteo");}
    }

    public Task<ContentSnapshot> GetNewsAsync(bool ai,CancellationToken ct)=>GetRssAsync(ai?"最新AI新闻":"资讯与热榜",ai?"人工智能 AI 科技":"中国 热点 新闻",ai?"AI新闻聚合":"资讯聚合",ct);

    public async Task<ContentSnapshot> GetBilibiliAsync(CancellationToken ct)
    {
        try
        {
            using var doc=await Client().GetFromJsonAsync<JsonDocument>("https://api.bilibili.com/x/web-interface/ranking/v2?rid=0&type=all",ct);var rows=new List<ContentRow>();
            if(doc!.RootElement.GetProperty("code").GetInt32()!=0)throw new InvalidOperationException("Bilibili API returned an error.");
            foreach(var item in doc.RootElement.GetProperty("data").GetProperty("list").EnumerateArray().Take(9))
            {var stat=item.GetProperty("stat");rows.Add(new(Trim(item.GetProperty("title").GetString()??"-",24),Trim(item.GetProperty("owner").GetProperty("name").GetString()??"-",10),$"播放 {Compact(stat.GetProperty("view").GetInt64())}"));}
            return new("B站热点视频",DateTimeOffset.Now,new Dictionary<string,string>{{"榜单","全站热门"},{"视频",rows.Count.ToString()}},rows,"B站全站热门排行","哔哩哔哩");
        }
        catch(Exception ex) when(!ct.IsCancellationRequested){logger.LogWarning(ex,"Bilibili panel source unavailable.");return Unavailable("B站热点视频","B站榜单暂不可用","哔哩哔哩");}
    }

    private async Task<ContentSnapshot> GetRssAsync(string title,string query,string label,CancellationToken ct)
    {
        try
        {
            var xml=await Client().GetStringAsync($"https://www.bing.com/news/search?q={Uri.EscapeDataString(query)}&format=rss&setlang=zh-cn",ct);var doc=XDocument.Parse(xml);var rows=doc.Descendants("item").Take(9).Select(x=>new ContentRow(Trim((string?)x.Element("title")??"-",26),Trim((string?)x.Element("source")??label,10),FormatDate((string?)x.Element("pubDate")))).ToArray();
            if(rows.Length==0)throw new InvalidOperationException("Empty RSS feed.");return new(title,DateTimeOffset.Now,new Dictionary<string,string>{{"栏目",label},{"条目",rows.Length.ToString()}},rows,"最新公开资讯快照","Bing News RSS");
        }
        catch(Exception ex) when(!ct.IsCancellationRequested){logger.LogWarning(ex,"Content panel source unavailable: {Title}",title);return Unavailable(title,"资讯源暂不可用","Bing News RSS");}
    }
    private HttpClient Client(){var h=clients.CreateClient();h.DefaultRequestHeaders.UserAgent.ParseAdd("AirPageSystem/0.0.8");return h;}
    private static ContentSnapshot Unavailable(string title,string status,string source)=>new(title,DateTimeOffset.Now,new Dictionary<string,string>{{"状态",status}},[],status,source);
    private static (string,double,double) WeatherSettings(string? json){try{using var d=JsonDocument.Parse(json??"{}");var r=d.RootElement;return(Str(r,"city","北京"),Num(r,"latitude",39.9042),Num(r,"longitude",116.4074));}catch{return("北京",39.9042,116.4074);}}
    private static double Num(JsonElement e,string n,double fallback=0)=>e.TryGetProperty(n,out var x)&&x.ValueKind==JsonValueKind.Number?x.GetDouble():fallback;
    private static string Str(JsonElement e,string n,string fallback)=>e.TryGetProperty(n,out var x)&&x.ValueKind==JsonValueKind.String?x.GetString()??fallback:fallback;
    private static string Trim(string s,int n)=>s.Length<=n?s:s[..(n-1)]+"…";
    private static string Compact(long n)=>n>=100_000_000?$"{n/100_000_000d:0.#}亿":n>=10_000?$"{n/10_000d:0.#}万":n.ToString();
    private static string FormatDate(string? value)=>DateTimeOffset.TryParse(value,out var d)?d.ToLocalTime().ToString("MM-dd HH:mm"):"刚刚";
}

public sealed record ContentSnapshot(string Title,DateTimeOffset CollectedAt,IReadOnlyDictionary<string,string> Metrics,IReadOnlyList<ContentRow> Rows,string Status,string Source);
public sealed record ContentRow(string Title,string Detail,string Value);
