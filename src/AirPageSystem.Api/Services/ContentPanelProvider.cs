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
            return new("天气与环境",ChinaTime.Now,metrics,rows,"实时天气与未来5日预报","Open-Meteo");
        }
        catch(Exception ex) when(!ct.IsCancellationRequested){logger.LogWarning(ex,"Weather panel source unavailable.");return Unavailable("天气与环境","天气数据暂不可用","Open-Meteo");}
    }

    public Task<ContentSnapshot> GetNewsAsync(bool ai,CancellationToken ct)=>GetNewsSourcesAsync(ai,ct);

    public async Task<ContentSnapshot> GetBilibiliAsync(CancellationToken ct)
    {
        try
        {
            using var doc=await Client().GetFromJsonAsync<JsonDocument>("https://api.bilibili.com/x/web-interface/ranking/v2?rid=0&type=all",ct);var rows=new List<ContentRow>();
            if(doc!.RootElement.GetProperty("code").GetInt32()!=0)throw new InvalidOperationException("Bilibili API returned an error.");
            foreach(var item in doc.RootElement.GetProperty("data").GetProperty("list").EnumerateArray().Take(9))
            {var stat=item.GetProperty("stat");rows.Add(new(Trim(item.GetProperty("title").GetString()??"-",24),Trim(item.GetProperty("owner").GetProperty("name").GetString()??"-",10),$"播放 {Compact(stat.GetProperty("view").GetInt64())}"));}
            return new("B站热点视频",ChinaTime.Now,new Dictionary<string,string>{{"榜单","全站热门"},{"视频",rows.Count.ToString()}},rows,"B站全站热门排行","哔哩哔哩");
        }
        catch(Exception ex) when(!ct.IsCancellationRequested){logger.LogWarning(ex,"Bilibili panel source unavailable.");return Unavailable("B站热点视频","B站榜单暂不可用","哔哩哔哩");}
    }

    private async Task<ContentSnapshot> GetNewsSourcesAsync(bool ai,CancellationToken ct)
    {
        var title=ai?"最新AI新闻":"资讯与热榜";var rows=new List<ContentRow>();var sources=new List<string>();
        var feeds=ai
            ? new[]{("量子位","https://www.qbitai.com/feed"),("36氪","https://36kr.com/feed"),("新浪财经","https://rss.sina.com.cn/news/finance/hot_roll.xml")}
            : new[]{("新浪财经","https://rss.sina.com.cn/news/finance/hot_roll.xml"),("36氪","https://36kr.com/feed"),("量子位","https://www.qbitai.com/feed")};
        foreach(var (source,url) in feeds)
        {
            try
            {
                using var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.Accept.ParseAdd("application/rss+xml, application/xml, text/xml, */*");using var response=await Client().SendAsync(request,ct);response.EnsureSuccessStatusCode();var xml=await response.Content.ReadAsStringAsync(ct);var doc=XDocument.Parse(xml);var found=doc.Descendants().Where(x=>x.Name.LocalName=="item").Select(x=>new {Title=Child(x,"title"),Date=Child(x,"pubDate")}).Where(x=>!string.IsNullOrWhiteSpace(x.Title)&&Matches(x.Title,ai)).Take(7).Select(x=>new ContentRow(Trim(x.Title,28),source,FormatDate(x.Date))).ToArray();
                if(found.Length>0){rows.AddRange(found);sources.Add(source);}
            }
            catch(Exception ex) when(!ct.IsCancellationRequested){logger.LogWarning(ex,"News feed unavailable: {Source}",source);}
        }
        var unique=rows.GroupBy(x=>x.Title).Select(x=>x.First()).OrderByDescending(x=>x.Value).Take(9).ToArray();if(unique.Length==0)return Unavailable(title,"金融与AI资讯源暂不可用",string.Join("、",feeds.Select(x=>x.Item1)));
        var label=ai?"AI / 大模型 / 算力":"金融 / AI / 科技";return new(title,ChinaTime.Now,new Dictionary<string,string>{{"关注",label},{"资讯",unique.Length.ToString()},{"来源",sources.Distinct().Count().ToString()}},unique,"金融与AI行业资讯聚合",string.Join("、",sources.Distinct()));
    }
    private HttpClient Client(){var h=clients.CreateClient();h.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 AirPageSystem/0.0.9");h.Timeout=TimeSpan.FromSeconds(12);return h;}
    private static ContentSnapshot Unavailable(string title,string status,string source)=>new(title,ChinaTime.Now,new Dictionary<string,string>{{"状态",status}},[],status,source);
    private static (string,double,double) WeatherSettings(string? json){try{using var d=JsonDocument.Parse(json??"{}");var r=d.RootElement;return(Str(r,"city","北京"),Num(r,"latitude",39.9042),Num(r,"longitude",116.4074));}catch{return("北京",39.9042,116.4074);}}
    private static double Num(JsonElement e,string n,double fallback=0)=>e.TryGetProperty(n,out var x)&&x.ValueKind==JsonValueKind.Number?x.GetDouble():fallback;
    private static string Str(JsonElement e,string n,string fallback)=>e.TryGetProperty(n,out var x)&&x.ValueKind==JsonValueKind.String?x.GetString()??fallback:fallback;
    private static string Trim(string s,int n)=>s.Length<=n?s:s[..(n-1)]+"…";
    private static string Compact(long n)=>n>=100_000_000?$"{n/100_000_000d:0.#}亿":n>=10_000?$"{n/10_000d:0.#}万":n.ToString();
    private static string FormatDate(string? value)=>DateTimeOffset.TryParse(value,out var d)?ChinaTime.Convert(d).ToString("MM-dd HH:mm"):"时间未知";
    private static string Child(XElement item,string name)=>item.Elements().FirstOrDefault(x=>x.Name.LocalName==name)?.Value.Trim()??"";
    private static bool Matches(string title,bool ai)
    {
        var all=new[]{"AI","人工智能","大模型","模型","芯片","算力","机器人","金融","证券","基金","股票","A股","港股","美股","银行","保险","经济","央行","融资","科技"};var aiOnly=new[]{"AI","人工智能","大模型","模型","芯片","算力","机器人","OpenAI","英伟达","科技"};return (ai?aiOnly:all).Any(x=>title.Contains(x,StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record ContentSnapshot(string Title,DateTimeOffset CollectedAt,IReadOnlyDictionary<string,string> Metrics,IReadOnlyList<ContentRow> Rows,string Status,string Source);
public sealed record ContentRow(string Title,string Detail,string Value);
