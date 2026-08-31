using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace AirPageSystem.Api.Services;

public sealed class SystemStatusProvider
{
    private readonly DateTimeOffset _started = DateTimeOffset.UtcNow;
    private long _lastRx, _lastTx;

    public async Task<SystemStatusSnapshot> GetAsync(CancellationToken ct)
    {
        var process = Process.GetCurrentProcess();
        var drives = DriveInfo.GetDrives().Where(x => x.IsReady).ToArray();
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(x => x.OperationalStatus == OperationalStatus.Up && x.NetworkInterfaceType != NetworkInterfaceType.Loopback).ToArray();
        var rx = interfaces.Sum(x => x.GetIPv4Statistics().BytesReceived);
        var tx = interfaces.Sum(x => x.GetIPv4Statistics().BytesSent);
        var previousRx = Interlocked.Exchange(ref _lastRx, rx);
        var previousTx = Interlocked.Exchange(ref _lastTx, tx);
        var deltaRx = previousRx == 0 ? 0 : Math.Max(0, rx - previousRx);
        var deltaTx = previousTx == 0 ? 0 : Math.Max(0, tx - previousTx);
        var top = Process.GetProcesses().Select(p =>
        {
            try { return new ProcessItem(p.ProcessName, p.Id, p.WorkingSet64, p.TotalProcessorTime.TotalSeconds); }
            catch { return null; }
        }).Where(x => x is not null).OrderByDescending(x => x!.MemoryBytes).Take(8).Cast<ProcessItem>().ToArray();
        await Task.Yield();
        return new(Environment.MachineName, RuntimeInformation.OSDescription, DateTimeOffset.UtcNow - _started,
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, GC.GetTotalMemory(false), process.WorkingSet64,
            drives.Sum(x => x.TotalSize), drives.Sum(x => x.TotalFreeSpace), rx, tx, deltaRx, deltaTx, top, ChinaTime.Now);
    }
}
public sealed record SystemStatusSnapshot(string Host, string OS, TimeSpan Uptime, long AvailableMemoryBytes,
    long ManagedMemoryBytes, long AppWorkingSetBytes, long DiskTotalBytes, long DiskFreeBytes,
    long NetworkReceivedBytes, long NetworkSentBytes, long RecentReceivedBytes, long RecentSentBytes,
    IReadOnlyList<ProcessItem> Processes, DateTimeOffset CollectedAt);
public sealed record ProcessItem(string Name, int Id, long MemoryBytes, double CpuSeconds);
