using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AirPageSystem.Api.Services;

public sealed class ThreeXUiMonitorProvider(IConfiguration configuration, ILogger<ThreeXUiMonitorProvider> logger)
{
    public async Task<ThreeXUiSnapshot> GetAsync(CancellationToken ct)
    {
        var processes = Process.GetProcesses();
        try
        {
            var panelProcesses = processes.Where(IsPanelProcess).ToArray();
            var xrayProcesses = processes.Where(p => Contains(p.ProcessName, "xray")).ToArray();
            var dbPath = FindDatabase();
            var database = dbPath is null ? new DatabaseSnapshot() : await ReadDatabaseAsync(dbPath, ct);
            var ports = database.Inbounds.Select(x => x.Port).Where(x => x is > 0 and <= 65535).ToHashSet();
            var (tcp, udp) = CountConnections(ports);

            return new ThreeXUiSnapshot(
                panelProcesses.Length > 0,
                xrayProcesses.Length > 0,
                VersionOf(panelProcesses, configuration["ThreeXUi:PanelVersion"]),
                VersionOf(xrayProcesses, configuration["ThreeXUi:XrayVersion"]),
                UptimeOf(panelProcesses.Concat(xrayProcesses)),
                UptimeOf(panelProcesses),UptimeOf(xrayProcesses),
                database.UpBytes,
                database.DownBytes,
                database.OutboundUpBytes,database.OutboundDownBytes,
                database.InboundCount,
                database.EnabledInboundCount,
                database.ClientCount,
                database.ClientIpCount,
                database.EnabledClientCount,database.RecentActiveClientCount,database.ExpiredClientCount,database.ClientTrafficBytes,database.ClientQuotaBytes,
                tcp,
                udp,
                LocalAddresses(),
                database.Inbounds,
                DateTimeOffset.Now,
                dbPath is null ? "未找到3x-ui数据库" : database.Status);
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    private string? FindDatabase()
    {
        var configured = configuration["ThreeXUi:DatabasePath"];
        var environment = Environment.GetEnvironmentVariable("XUI_DB_PATH");
        var candidates = new[]
        {
            configured, environment, "/etc/x-ui/x-ui.db", "/usr/local/x-ui/x-ui.db",
            "/usr/local/x-ui/db/x-ui.db", Path.Combine(AppContext.BaseDirectory, "data", "x-ui.db")
        };
        return candidates.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && File.Exists(x));
    }

    private async Task<DatabaseSnapshot> ReadDatabaseAsync(string path, CancellationToken ct)
    {
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Cache = SqliteCacheMode.Private, Pooling = false }.ToString();
            await using var connection = new SqliteConnection(cs);
            await connection.OpenAsync(ct);
            if (!await HasTableAsync(connection, "inbounds", ct)) return new DatabaseSnapshot(Status: "数据库缺少inbounds表");

            var inbounds = new List<ThreeXUiInbound>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT remark, protocol, port, up, down, enable FROM inbounds ORDER BY (up + down) DESC";
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    inbounds.Add(new ThreeXUiInbound(
                        Text(reader, 0, "未命名"), Text(reader, 1, "-"), Number(reader, 2),
                        Number64(reader, 3), Number64(reader, 4), Number(reader, 5) != 0));
            }

            var clients = await CountDistinctAsync(connection, "client_traffics", "email", ct);
            if (await HasTableAsync(connection, "clients", ct)) clients = await CountAsync(connection, "clients", ct);
            var ips = await CountClientIpsAsync(connection, ct);
            var clientStats=await ReadClientStatsAsync(connection,ct);
            var outbound=await SumTrafficAsync(connection,"outbound_traffics",ct);
            return new DatabaseSnapshot(inbounds.Sum(x => x.UpBytes), inbounds.Sum(x => x.DownBytes),
                inbounds.Count, inbounds.Count(x => x.Enabled), clients, ips,outbound.Up,outbound.Down,clientStats.Enabled,clientStats.Recent,clientStats.Expired,clientStats.Used,clientStats.Quota,inbounds, "v3.7兼容只读采集");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to read the local 3x-ui monitoring database.");
            return new DatabaseSnapshot(Status: "数据库读取失败");
        }
    }

    private static async Task<(long Up,long Down)> SumTrafficAsync(SqliteConnection connection,string table,CancellationToken ct)
    {
        if(!await HasTableAsync(connection,table,ct))return(0,0);await using var command=connection.CreateCommand();command.CommandText=$"SELECT COALESCE(SUM(up),0),COALESCE(SUM(down),0) FROM \"{table}\"";await using var reader=await command.ExecuteReaderAsync(ct);return await reader.ReadAsync(ct)?(Number64(reader,0),Number64(reader,1)):(0,0);
    }
    private static async Task<(int Enabled,int Recent,int Expired,long Used,long Quota)> ReadClientStatsAsync(SqliteConnection connection,CancellationToken ct)
    {
        if(!await HasTableAsync(connection,"client_traffics",ct))return(0,0,0,0,0);var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();var recent=now-180_000;
        await using var command=connection.CreateCommand();command.CommandText="SELECT COALESCE(SUM(CASE WHEN enable<>0 THEN 1 ELSE 0 END),0),COALESCE(SUM(CASE WHEN last_online >= $recent THEN 1 ELSE 0 END),0),COALESCE(SUM(CASE WHEN expiry_time>0 AND expiry_time<$now THEN 1 ELSE 0 END),0),COALESCE(SUM(up+down),0),COALESCE(SUM(total),0) FROM client_traffics";command.Parameters.AddWithValue("$recent",recent);command.Parameters.AddWithValue("$now",now);await using var reader=await command.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct))return(0,0,0,0,0);return(Number(reader,0),Number(reader,1),Number(reader,2),Number64(reader,3),Number64(reader,4));
    }

    private static async Task<bool> HasTableAsync(SqliteConnection connection, string table, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name";
        command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
    }

    private static async Task<int> CountAsync(SqliteConnection connection, string table, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    private static async Task<int> CountDistinctAsync(SqliteConnection connection, string table, string column, CancellationToken ct)
    {
        if (!await HasTableAsync(connection, table, ct)) return 0;
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(DISTINCT \"{column}\") FROM \"{table}\"";
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    private static async Task<int> CountClientIpsAsync(SqliteConnection connection, CancellationToken ct)
    {
        if (!await HasTableAsync(connection, "inbound_client_ips", ct)) return 0;
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ips FROM inbound_client_ips";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var value = reader.IsDBNull(0) ? "" : reader.GetString(0);
            try
            {
                foreach (var ip in JsonSerializer.Deserialize<string[]>(value) ?? []) unique.Add(ip);
            }
            catch (JsonException)
            {
                foreach (var ip in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) unique.Add(ip);
            }
        }
        return unique.Count;
    }

    private static (int Tcp, int Udp) CountConnections(HashSet<int> ports)
    {
        if (ports.Count == 0) return (0, 0);
        try
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var tcp = properties.GetActiveTcpConnections().Count(x => ports.Contains(x.LocalEndPoint.Port));
            var udp = properties.GetActiveUdpListeners().Count(x => ports.Contains(x.Port));
            return (tcp, udp);
        }
        catch { return (0, 0); }
    }

    private static IReadOnlyList<string> LocalAddresses()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(x => x.OperationalStatus == OperationalStatus.Up)
                .SelectMany(x => x.GetIPProperties().UnicastAddresses)
                .Select(x => x.Address)
                .Where(x => x.AddressFamily == AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(x))
                .Select(x => x.ToString()).Distinct().Take(2).ToArray();
        }
        catch { return []; }
    }

    private static bool IsPanelProcess(Process process) =>
        Contains(process.ProcessName, "x-ui") || Contains(process.ProcessName, "3x-ui") ||
        string.Equals(process.ProcessName, "xui", StringComparison.OrdinalIgnoreCase);
    private static bool Contains(string value, string part) => value.Contains(part, StringComparison.OrdinalIgnoreCase);
    private static string VersionOf(IEnumerable<Process> processes, string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        foreach (var process in processes)
            try
            {
                var version = process.MainModule?.FileVersionInfo.ProductVersion ?? process.MainModule?.FileVersionInfo.FileVersion;
                if (!string.IsNullOrWhiteSpace(version)) return version.Split('+')[0];
            }
            catch { }
        return "未知";
    }
    private static TimeSpan UptimeOf(IEnumerable<Process> processes)
    {
        var starts = new List<DateTime>();
        foreach (var process in processes) try { starts.Add(process.StartTime.ToUniversalTime()); } catch { }
        return starts.Count == 0 ? TimeSpan.Zero : DateTime.UtcNow - starts.Min();
    }
    private static string Text(SqliteDataReader reader, int ordinal, string fallback) => reader.IsDBNull(ordinal) ? fallback : reader.GetString(ordinal);
    private static int Number(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    private static long Number64(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? 0 : Convert.ToInt64(reader.GetValue(ordinal));
}

internal sealed record DatabaseSnapshot(long UpBytes = 0, long DownBytes = 0, int InboundCount = 0,
    int EnabledInboundCount = 0, int ClientCount = 0, int ClientIpCount = 0,long OutboundUpBytes=0,long OutboundDownBytes=0,
    int EnabledClientCount=0,int RecentActiveClientCount=0,int ExpiredClientCount=0,long ClientTrafficBytes=0,long ClientQuotaBytes=0,
    IReadOnlyList<ThreeXUiInbound>? Rows = null, string Status = "无数据")
{
    public IReadOnlyList<ThreeXUiInbound> Inbounds { get; } = Rows ?? [];
}

public sealed record ThreeXUiSnapshot(bool PanelRunning, bool XrayRunning, string PanelVersion, string XrayVersion,
    TimeSpan Uptime,TimeSpan PanelUptime,TimeSpan XrayUptime,long UpBytes, long DownBytes,long OutboundUpBytes,long OutboundDownBytes,int InboundCount, int EnabledInboundCount, int ClientCount,
    int ClientIpCount,int EnabledClientCount,int RecentActiveClientCount,int ExpiredClientCount,long ClientTrafficBytes,long ClientQuotaBytes,int TcpConnections, int UdpListeners, IReadOnlyList<string> Addresses,
    IReadOnlyList<ThreeXUiInbound> Inbounds, DateTimeOffset CollectedAt, string Status);
public sealed record ThreeXUiInbound(string Remark, string Protocol, int Port, long UpBytes, long DownBytes, bool Enabled);
