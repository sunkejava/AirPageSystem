using AirPageSystem.Api.Contracts;
using AirPageSystem.Api.Data;
using AirPageSystem.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Controllers;
[ApiController,Route("api")]
public sealed class PanelsController(PanelExecutionService executor,AppDbContext db,IWebHostEnvironment env,IConfiguration config):ControllerBase
{
 [HttpPost("panels/execute")] public async Task<IActionResult> Execute(ExecutePanelRequest r,CancellationToken ct)
 {var x=await executor.ExecuteAsync(r.TemplateId,r.DeviceId,r.Push,ct);return Ok(new{x.RecordId,x.PreviewPath,bmpBytes=x.Bmp.Length,pngBytes=x.Png.Length,x.Push});}
 [HttpGet("history")] public async Task<IActionResult> History(CancellationToken ct)=>Ok(await db.PushRecords.OrderByDescending(x=>x.CreatedAt).Take(100).ToListAsync(ct));
 [HttpGet("dashboard")] public async Task<IActionResult> Dashboard(CancellationToken ct)=>Ok(new{devices=await db.Devices.CountAsync(ct),templates=await db.Templates.CountAsync(ct),schedules=await db.Schedules.CountAsync(x=>x.Enabled,ct),pushesToday=await db.PushRecords.CountAsync(x=>x.CreatedAt>=new DateTimeOffset(DateTime.UtcNow.Date,TimeSpan.Zero),ct),latest=await db.PushRecords.OrderByDescending(x=>x.CreatedAt).Take(8).ToListAsync(ct)});
 [HttpGet("renders/{file}")] public IActionResult Render(string file)
 {
  var safe=Path.GetFileName(file);var path=Path.Combine(env.ContentRootPath,config["Panel:OutputDirectory"]??"data/renders",safe);
  return System.IO.File.Exists(path)?PhysicalFile(path,"image/png"):NotFound();
 }
}
