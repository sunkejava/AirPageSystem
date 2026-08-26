using AirPageSystem.Api.Data;
using Cronos;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Services;

public sealed class ScheduleWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<ScheduleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, config.GetValue("Scheduler:PollingSeconds", 15)));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await TickAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Scheduler tick failed."); }
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
                var cron = CronExpression.Parse(job.Cron, CronFormat.Standard);
                var zone = TimeZoneInfo.FindSystemTimeZoneById(job.TimeZoneId);
                if (job.NextRunAt is null)
                {
                    job.NextRunAt = cron.GetNextOccurrence(now, zone);
                    continue;
                }
                var result = await executor.ExecuteAsync(job.TemplateId, job.DeviceId, true, ct);
                job.LastRunAt = now; job.LastResult = result.Push.Message;
                job.NextRunAt = cron.GetNextOccurrence(now, zone);
            }
            catch (Exception ex)
            {
                job.LastRunAt = now; job.LastResult = ex.Message;
                try { job.NextRunAt = CronExpression.Parse(job.Cron).GetNextOccurrence(now.AddMinutes(1), TimeZoneInfo.FindSystemTimeZoneById(job.TimeZoneId)); }
                catch { job.Enabled = false; }
                logger.LogError(ex, "Schedule {ScheduleId} failed.", job.Id);
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
