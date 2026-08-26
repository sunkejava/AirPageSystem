using System.Buffers.Binary;
using System.Text.Json;
using AirPageSystem.Api.Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace AirPageSystem.Api.Services;

public sealed class PanelRenderer(IConfiguration config)
{
    private readonly int _width = config.GetValue("Panel:Width", 528);
    private readonly int _height = config.GetValue("Panel:Height", 792);
    private readonly string _fontPath = ResolveFontPath(config);

    private static string ResolveFontPath(IConfiguration config)
    {
        var configured = config["Panel:FontPath"];
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        var bundled = Path.Combine(AppContext.BaseDirectory, "fonts", "NotoSansCJK-Regular.ttc");
        return File.Exists(bundled) ? bundled : "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc";
    }

    public RenderedPanel RenderMarket(MarketSnapshot m) => Render(canvas =>
    {
        Header(canvas, "A股盘中快照", m.CollectedAt, "盘中快照");
        Text(canvas, "主要指数", 18, 96, 24, true);
        for (var i = 0; i < Math.Min(3, m.Indices.Count); i++)
        {
            var x = m.Indices[i]; var left = 18 + i * 170;
            Box(canvas, left, 136, 152, 80);
            Center(canvas, x.Name, left + 76, 146, 17);
            Center(canvas, x.Price.ToString("0.00"), left + 76, 172, 21);
            Center(canvas, $"{x.ChangePercent:+0.00;-0.00;0.00}%", left + 76, 199, 17);
        }
        Text(canvas, "市场广度", 18, 236, 24, true);
        Text(canvas, $"上涨 {m.Advancers}   下跌 {m.Decliners}   平盘 {m.Unchanged}", 24, 278, 20);
        Text(canvas, $"涨停 {m.LimitUp}     跌停 {m.LimitDown}", 24, 312, 20);
        Text(canvas, "异动股票", 18, 354, 24, true);
        Text(canvas, "代码   名称        现价    涨跌幅   振幅", 18, 397, 16);
        var rows = m.Gainers.Take(4).Concat(m.Losers.Take(4)).ToArray();
        for (var i = 0; i < rows.Length; i++)
        {
            var y = 428 + i * 37; var x = rows[i];
            if (i % 2 == 0) canvas.Mutate(c => c.Fill(Color.ParseHex("EEEEEE"), new Rectangle(14, y - 3, 500, 33)));
            Text(canvas, x.Code, 18, y, 16); Text(canvas, x.Name[..Math.Min(6, x.Name.Length)], 98, y, 16);
            Text(canvas, x.Price.ToString("0.00"), 226, y, 16);
            Text(canvas, $"{x.ChangePercent:+0.00;-0.00;0.00}%", 326, y, 16);
            Text(canvas, $"{x.AmplitudePercent:0.00}%", 430, y, 16);
        }
        Footer(canvas, "实时行情会波动｜仅供参考，不构成投资建议", "来源：腾讯证券、东方财富");
    });

    public RenderedPanel RenderSystem(SystemStatusSnapshot s) => Render(canvas =>
    {
        Header(canvas, "服务端状态面板", s.CollectedAt);
        Text(canvas, s.Host, 18, 96, 26, true);
        Text(canvas, Trim(s.OS, 42), 18, 130, 15);
        var memPct = Percent(s.AppWorkingSetBytes, s.AvailableMemoryBytes);
        var diskPct = Percent(s.DiskTotalBytes - s.DiskFreeBytes, s.DiskTotalBytes);
        Metric(canvas, 18, 172, "应用内存", Bytes(s.AppWorkingSetBytes), $"{memPct:0.0}%");
        Metric(canvas, 184, 172, "磁盘使用", Bytes(s.DiskTotalBytes - s.DiskFreeBytes), $"{diskPct:0.0}%");
        Metric(canvas, 350, 172, "运行时间", $"{(int)s.Uptime.TotalHours}h", $"{s.Uptime.Minutes}m");
        Text(canvas, "网络流量", 18, 274, 24, true);
        Text(canvas, $"接收累计 {Bytes(s.NetworkReceivedBytes)}    本周期 +{Bytes(s.RecentReceivedBytes)}", 24, 314, 18);
        Text(canvas, $"发送累计 {Bytes(s.NetworkSentBytes)}    本周期 +{Bytes(s.RecentSentBytes)}", 24, 346, 18);
        Text(canvas, "内存占用较高的进程", 18, 392, 24, true);
        Text(canvas, "PID      进程名称                     内存", 20, 432, 16);
        for (var i = 0; i < Math.Min(7, s.Processes.Count); i++)
        {
            var p = s.Processes[i]; var y = 464 + i * 36;
            if (i % 2 == 0) canvas.Mutate(c => c.Fill(Color.ParseHex("EEEEEE"), new Rectangle(14, y - 3, 500, 32)));
            Text(canvas, p.Id.ToString(), 20, y, 16);
            Text(canvas, Trim(p.Name, 24), 100, y, 16);
            Text(canvas, Bytes(p.MemoryBytes), 412, y, 16);
        }
        Footer(canvas, "系统状态实时采集｜网络增量按采集周期统计", $"应用托管内存 {Bytes(s.ManagedMemoryBytes)}");
    });

    public RenderedPanel RenderThreeXUi(ThreeXUiSnapshot s) => Render(canvas =>
    {
        Header(canvas, "3x-ui 代理监控", s.CollectedAt, "实时快照");
        var state = $"面板 {(s.PanelRunning ? "运行" : "停止")}   Xray {(s.XrayRunning ? "运行" : "停止")}";
        Text(canvas, state, 18, 98, 24, true);
        Text(canvas, $"3x-ui {s.PanelVersion}   Xray {s.XrayVersion}", 18, 132, 15);
        Metric(canvas, 18, 170, "总用量", Bytes(s.UpBytes + s.DownBytes), $"运行 {Duration(s.Uptime)}");
        Metric(canvas, 184, 170, "入站", $"{s.EnabledInboundCount}/{s.InboundCount}", "启用/全部");
        Metric(canvas, 350, 170, "客户端", s.ClientCount.ToString(), $"IP {s.ClientIpCount}");
        Text(canvas, "流量与连接", 18, 270, 24, true);
        Text(canvas, $"上传 {Bytes(s.UpBytes)}    下载 {Bytes(s.DownBytes)}", 24, 310, 19);
        Text(canvas, $"TCP连接 {s.TcpConnections}    UDP监听 {s.UdpListeners}", 24, 344, 18);
        Text(canvas, $"主机IP {Trim(string.Join(" / ", s.Addresses.DefaultIfEmpty("未获取")), 38)}", 24, 376, 17);
        Text(canvas, "高流量入站", 18, 416, 24, true);
        Text(canvas, "状态  名称/协议          端口      上传      下载", 18, 456, 15);
        for (var i = 0; i < Math.Min(6, s.Inbounds.Count); i++)
        {
            var item = s.Inbounds[i]; var y = 488 + i * 35;
            if (i % 2 == 0) canvas.Mutate(c => c.Fill(Color.ParseHex("EEEEEE"), new Rectangle(14, y - 3, 500, 31)));
            Text(canvas, item.Enabled ? "●" : "○", 20, y, 15);
            Text(canvas, Trim($"{item.Remark}/{item.Protocol}", 17), 52, y, 15);
            Text(canvas, item.Port.ToString(), 260, y, 15);
            Text(canvas, Bytes(item.UpBytes), 328, y, 15);
            Text(canvas, Bytes(item.DownBytes), 425, y, 15);
        }
        Footer(canvas, $"只读采集｜{s.Status}", "不读取UUID、密码或订阅凭据");
    });

    public RenderedPanel RenderCustom(string title, IReadOnlyDictionary<string, string> metrics,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows, DateTimeOffset collectedAt) => Render(canvas =>
    {
        Header(canvas, title, collectedAt);
        var y = 108;
        foreach (var metric in metrics.Take(6))
        {
            Text(canvas, metric.Key, 24, y, 18); Text(canvas, Trim(metric.Value, 26), 250, y, 22, true); y += 48;
        }
        Text(canvas, "明细", 18, y + 12, 24, true); y += 58;
        foreach (var row in rows.Take(9))
        {
            var line = string.Join("  ", row.Take(3).Select(x => $"{x.Key}:{x.Value}"));
            Text(canvas, Trim(line, 46), 20, y, 17); y += 40;
        }
        Footer(canvas, "自定义数据源快照", "由 AirPageSystem 自动生成");
    });

    private RenderedPanel Render(Action<Image<L8>> draw)
    {
        using var image = new Image<L8>(_width, _height, new L8(255)); draw(image);
        using var png = new MemoryStream();
        image.Save(png, new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression });
        var bmp = Encode2BitBmp(image);
        return new(bmp, png.ToArray(), _width, _height);
    }
    private Font Font(int size, FontStyle style = FontStyle.Regular)
    {
        var collection = new FontCollection();
        if (File.Exists(_fontPath)) return collection.Add(_fontPath).CreateFont(size, style);
        var families = SystemFonts.Families.ToArray();
        var family = families.FirstOrDefault(x => x.Name.Contains("Noto", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(family.Name)) family = families.First();
        return family.CreateFont(size, style);
    }
    private void Header(Image<L8> c, string title, DateTimeOffset at, string snapshot = "状态快照")
    {
        c.Mutate(x => x.Fill(Color.Black, new Rectangle(0, 0, _width, 82)));
        Text(c, title, 18, 10, 34, true, Color.White);
        Text(c, $"{at:yyyy-MM-dd HH:mm:ss} 北京时间｜{snapshot}", 18, 56, 14, false, Color.ParseHex("DDDDDD"));
    }
    private void Footer(Image<L8> c, string first, string second)
    {
        c.Mutate(x => x.Fill(Color.ParseHex("191919"), new Rectangle(0, _height - 55, _width, 55)));
        Text(c, first, 16, _height - 48, 15, false, Color.White);
        Text(c, second, 16, _height - 25, 13, false, Color.ParseHex("CCCCCC"));
    }
    private void Metric(Image<L8> c, int x, int y, string label, string value, string detail)
    {
        Box(c, x, y, 150, 82); Center(c, label, x + 75, y + 9, 16); Center(c, value, x + 75, y + 34, 21); Center(c, detail, x + 75, y + 62, 15);
    }
    private static void Box(Image<L8> c, int x, int y, int w, int h) =>
        c.Mutate(v => v.Fill(Color.ParseHex("EEEEEE"), new Rectangle(x, y, w, h)).Draw(Color.ParseHex("555555"), 2, new Rectangle(x, y, w, h)));
    private void Text(Image<L8> c, string value, float x, float y, int size, bool bold = false, Color? color = null) =>
        c.Mutate(v => v.DrawText(value, Font(size, bold ? FontStyle.Bold : FontStyle.Regular), color ?? Color.Black, new PointF(x, y)));
    private void Center(Image<L8> c, string value, float center, float y, int size)
    {
        var font = Font(size); var width = TextMeasurer.MeasureSize(value, new TextOptions(font)).Width;
        c.Mutate(v => v.DrawText(value, font, Color.Black, new PointF(center - width / 2, y)));
    }
    private static string Trim(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
    private static double Percent(long used, long total) => total <= 0 ? 0 : used * 100d / total;
    private static string Bytes(long bytes) => bytes switch
    {
        >= 1L << 40 => $"{bytes / (double)(1L << 40):0.0} TB",
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0.0} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):0.0} KB",
        _ => $"{bytes} B"
    };
    private static string Duration(TimeSpan value) => value == TimeSpan.Zero ? "未知" : value.TotalDays >= 1 ? $"{(int)value.TotalDays}天{value.Hours}时" : $"{(int)value.TotalHours}时{value.Minutes}分";
    private static byte[] Encode2BitBmp(Image<L8> image)
    {
        var width = image.Width; var height = image.Height; var rowBytes = ((width * 2 + 31) / 32) * 4;
        var offset = 14 + 40 + 16; var dataLength = rowBytes * height; var output = new byte[offset + dataLength];
        output[0] = (byte)'B'; output[1] = (byte)'M'; BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(2), output.Length);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(10), offset); BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(18), width); BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(22), height);
        BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(26), 1); BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(28), 2);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(34), dataLength); BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(46), 4);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(50), 4);
        for (var i = 0; i < 4; i++) { var v = (byte)(i * 85); output[54 + i * 4] = v; output[55 + i * 4] = v; output[56 + i * 4] = v; }
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < height; y++)
            {
                var src = accessor.GetRowSpan(height - 1 - y); var dst = output.AsSpan(offset + y * rowBytes, rowBytes);
                for (var x = 0; x < width; x++) dst[x / 4] |= (byte)(Math.Clamp((src[x].PackedValue + 42) / 85, 0, 3) << (6 - 2 * (x % 4)));
            }
        });
        return output;
    }
}
public sealed record RenderedPanel(byte[] Bmp, byte[] Png, int Width, int Height);
