using AirPageSystem.Api.Contracts;
using AirPageSystem.Api.Data;
using AirPageSystem.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Controllers;
[ApiController,Route("api")]
public sealed class PanelsController(PanelExecutionService executor,AppDbContext db,IWebHostEnvironment env,IConfiguration config,CurrentUser current):ControllerBase
{
 [HttpPost("panels/execute")] public async Task<IActionResult> Execute(ExecutePanelRequest r,CancellationToken ct)
 {await current.DemandAsync(r.Push?"panels.push":"menu.templates",ct);var x=await executor.ExecuteAsync(r.TemplateId,r.DeviceId,r.Push,ct,current.Id,r.RetryPolicyId);return Ok(new{x.RecordId,x.PreviewPath,bmpBytes=x.Bmp.Length,pngBytes=x.Png.Length,x.Push});}
 [HttpPost("panels/preview")]
 public async Task<IActionResult> Preview(PreviewTemplateRequest r,CancellationToken ct)
 {await current.DemandAsync("menu.templates",ct);var image=await executor.PreviewAsync(r.Name,r.Type,r.DataSourceId,r.SchemaJson,current.Id,ct);return File(image.Png,"image/png");}
 [HttpGet("history")] public async Task<IActionResult> History(CancellationToken ct)
 {
  // SQLite stores DateTimeOffset values but cannot translate ORDER BY for that CLR type.
  // Materialize first so existing v0.0.1 databases remain compatible without migration.
  var query=await db.PushRecords.AsNoTracking().VisibleToAsync(current,ct);var records=await query.ToListAsync(ct);
  return Ok(records.OrderByDescending(x=>x.CreatedAt).Take(100));
 }
 [HttpGet("dashboard")] public async Task<IActionResult> Dashboard(CancellationToken ct)
 {
  var query=await db.PushRecords.AsNoTracking().VisibleToAsync(current,ct);var records=await query.ToListAsync(ct);
  var today=new DateTimeOffset(DateTime.UtcNow.Date,TimeSpan.Zero);
  return Ok(new{devices=await db.Devices.CountAsync(x=>x.OwnerUserId==current.Id,ct),templates=await db.Templates.CountAsync(x=>!x.IsDeleted&&(x.OwnerUserId==null||x.OwnerUserId==current.Id),ct),schedules=await db.Schedules.CountAsync(x=>x.OwnerUserId==current.Id&&x.Enabled,ct),pushesToday=records.Count(x=>x.CreatedAt>=today),latest=records.OrderByDescending(x=>x.CreatedAt).Take(8)});
 }
 [HttpGet("renders/{file}")] public IActionResult Render(string file)
 {
  var safe=Path.GetFileName(file);var preview=$"/api/renders/{safe}";var visible=current.IsAdmin||db.PushRecords.Any(x=>x.PreviewPath==preview&&x.OwnerUserId==current.Id);if(!visible)return NotFound();var path=Path.Combine(env.ContentRootPath,config["Panel:OutputDirectory"]??"data/renders",safe);
  return System.IO.File.Exists(path)?PhysicalFile(path,"image/png"):NotFound();
 }
}
