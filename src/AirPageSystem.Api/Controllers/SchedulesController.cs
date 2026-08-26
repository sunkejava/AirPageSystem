using AirPageSystem.Api.Contracts;
using AirPageSystem.Api.Data;
using AirPageSystem.Api.Models;
using Cronos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Controllers;
[ApiController,Route("api/schedules")]
public sealed class SchedulesController(AppDbContext db):ControllerBase
{
 [HttpGet] public async Task<IActionResult> List(CancellationToken ct)=>Ok(await db.Schedules.ToListAsync(ct));
 [HttpPost] public async Task<IActionResult> Save(SaveScheduleRequest r,CancellationToken ct)
 {
  var cron=CronExpression.Parse(r.Cron,CronFormat.Standard);var zone=TimeZoneInfo.FindSystemTimeZoneById(r.TimeZoneId);
  var x=new ScheduleDefinition{Name=r.Name,TemplateId=r.TemplateId,DeviceId=r.DeviceId,Cron=r.Cron,TimeZoneId=r.TimeZoneId,Enabled=r.Enabled,NextRunAt=cron.GetNextOccurrence(DateTimeOffset.UtcNow,zone)};
  db.Add(x);await db.SaveChangesAsync(ct);return Ok(x);
 }
 [HttpPut("{id:guid}/toggle")] public async Task<IActionResult> Toggle(Guid id,CancellationToken ct){var x=await db.Schedules.FindAsync([id],ct);if(x is null)return NotFound();x.Enabled=!x.Enabled;await db.SaveChangesAsync(ct);return Ok(x);}
 [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id,CancellationToken ct){await db.Schedules.Where(x=>x.Id==id).ExecuteDeleteAsync(ct);return NoContent();}
}

