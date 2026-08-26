using AirPageSystem.Api.Data;
using Cronos;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Services;

public sealed class ScheduleWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<ScheduleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, config.GetValue("Scheduler:PollingSeconds", 15)));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Scheduler tick failed."); }
            await Task.Delay(interval, stoppingToken);
        }
    }
    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var executor = scope.ServiceProvider.GetRequiredService<PanelExecutionService>();
        var now = DateTimeOffset.UtcNow;
        // SQLite cannot translate DateTimeOffset comparisons. Keep the filtering in memory
        // so databases created by v0.0.1 continue to work without a destructive migration.
        var enabledJobs = await db.Schedules.Where(x => x.Enabled).ToListAsync(ct);
        var jobs = enabledJobs.Where(x => x.NextRunAt == null || x.NextRunAt <= now).ToArray();
        foreach (var job in jobs)
        {
            try
            {
                if (job.NextRunAt is null)
                {
                    job.NextRunAt = ScheduleTime.Next(job.Cron, job.TimeZoneId, now);
                    continue;
                }
                // Move the cursor and persist it before external I/O. This prevents a slow
                // upload or process restart from executing the same occurrence twice.
                job.NextRunAt = ScheduleTime.Next(job.Cron, job.TimeZoneId, now);
                job.LastRunAt = now; job.LastResult = "执行中";
                await db.SaveChangesAsync(ct);
                var result = await executor.ExecuteAsync(job.TemplateId, job.DeviceId, true, ct);
                job.LastRunAt = now; job.LastResult = result.Push.Message;
                logger.LogInformation("Schedule {ScheduleId} completed: {Result}", job.Id, result.Push.Message);
            }
            catch (Exception ex)
            {
                job.LastRunAt = now; job.LastResult = $"失败：{ex.Message}";
                try { job.NextRunAt = ScheduleTime.Next(job.Cron, job.TimeZoneId, now); }
                catch { job.Enabled = false; }
                logger.LogError(ex, "Schedule {ScheduleId} failed.", job.Id);
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
