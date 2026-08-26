using AirPageSystem.Api.Contracts;
using AirPageSystem.Api.Data;
using AirPageSystem.Api.Models;
using AirPageSystem.Api.Services;
using Cronos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Controllers;
[ApiController,Route("api/schedules")]
public sealed class SchedulesController(AppDbContext db, PanelExecutionService executor):ControllerBase
{
 [HttpGet] public async Task<IActionResult> List(CancellationToken ct)=>Ok(await db.Schedules.ToListAsync(ct));
 [HttpPost] public async Task<IActionResult> Save(SaveScheduleRequest r,CancellationToken ct)
 {
  var error=await ValidateReferences(r,ct);if(error is not null)return error;
  var x=new ScheduleDefinition{Name=r.Name,TemplateId=r.TemplateId,DeviceId=r.DeviceId,Cron=r.Cron,TimeZoneId=r.TimeZoneId,Enabled=r.Enabled,NextRunAt=ScheduleTime.Next(r.Cron,r.TimeZoneId,DateTimeOffset.UtcNow)};
  db.Add(x);await db.SaveChangesAsync(ct);return Ok(x);
 }
 [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id,SaveScheduleRequest r,CancellationToken ct)
 {
  var x=await db.Schedules.FindAsync([id],ct);if(x is null)return NotFound();
  var error=await ValidateReferences(r,ct);if(error is not null)return error;
  x.Name=r.Name;x.TemplateId=r.TemplateId;x.DeviceId=r.DeviceId;x.Cron=r.Cron;x.TimeZoneId=r.TimeZoneId;x.Enabled=r.Enabled;
  x.NextRunAt=ScheduleTime.Next(r.Cron,r.TimeZoneId,DateTimeOffset.UtcNow);x.LastResult="任务配置已更新";
  await db.SaveChangesAsync(ct);return Ok(x);
 }
 [HttpPut("{id:guid}/toggle")] public async Task<IActionResult> Toggle(Guid id,CancellationToken ct)
 {
  var x=await db.Schedules.FindAsync([id],ct);if(x is null)return NotFound();x.Enabled=!x.Enabled;
  if(x.Enabled)x.NextRunAt=ScheduleTime.Next(x.Cron,x.TimeZoneId,DateTimeOffset.UtcNow);
  await db.SaveChangesAsync(ct);return Ok(x);
 }
 [HttpPost("{id:guid}/run")] public async Task<IActionResult> Run(Guid id,CancellationToken ct)
 {
  var x=await db.Schedules.FindAsync([id],ct);if(x is null)return NotFound();
  var now=DateTimeOffset.UtcNow;x.LastRunAt=now;x.LastResult="测试执行中";await db.SaveChangesAsync(ct);
  try
  {
   var result=await executor.ExecuteAsync(x.TemplateId,x.DeviceId,true,ct);
   x.LastResult=result.Push.Message;await db.SaveChangesAsync(ct);
   return Ok(new{x.LastRunAt,x.LastResult,result.Push.Succeeded,result.Push.Refreshed});
  }
  catch(Exception ex)
  {
   x.LastResult=$"失败：{ex.Message}";await db.SaveChangesAsync(ct);throw;
  }
 }
 [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id,CancellationToken ct){await db.Schedules.Where(x=>x.Id==id).ExecuteDeleteAsync(ct);return NoContent();}

 private async Task<IActionResult?> ValidateReferences(SaveScheduleRequest r,CancellationToken ct)
 {
  try{_ = ScheduleTime.Next(r.Cron,r.TimeZoneId,DateTimeOffset.UtcNow);}catch(Exception ex){return BadRequest($"定时配置无效：{ex.Message}");}
  if(!await db.Templates.AnyAsync(x=>x.Id==r.TemplateId,ct))return BadRequest("面板模板不存在。");
  if(!await db.Devices.AnyAsync(x=>x.Id==r.DeviceId,ct))return BadRequest("AirPage设备不存在。");
  return null;
 }
}
