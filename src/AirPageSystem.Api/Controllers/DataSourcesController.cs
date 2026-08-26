using AirPageSystem.Api.Contracts;
using AirPageSystem.Api.Data;
using AirPageSystem.Api.Models;
using AirPageSystem.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

namespace AirPageSystem.Api.Controllers;
[ApiController,Route("api/data-sources")]
public sealed class DataSourcesController(AppDbContext db,CustomJsonDataProvider provider,IDataProtectionProvider protection):ControllerBase
{
 [HttpGet] public async Task<IActionResult> List(CancellationToken ct)=>Ok(await db.DataSources.Select(x=>new{x.Id,x.Name,x.Method,x.Url,x.Body,x.Enabled,hasProtectedHeaders=x.ProtectedHeadersJson!=null}).ToListAsync(ct));
 [HttpPost] public async Task<IActionResult> Save(SaveDataSourceRequest r,CancellationToken ct)
 {
  var protectedHeaders=string.IsNullOrWhiteSpace(r.HeadersJson)?null:protection.CreateProtector("AirPageSystem.DataSourceHeaders.v1").Protect(r.HeadersJson);
  var x=new DataSourceDefinition{Name=r.Name,Method=r.Method.ToUpperInvariant(),Url=r.Url,ProtectedHeadersJson=protectedHeaders,Body=r.Body,Enabled=r.Enabled};db.Add(x);await db.SaveChangesAsync(ct);return Ok(new{x.Id,x.Name,x.Method,x.Url,x.Enabled,hasProtectedHeaders=protectedHeaders!=null});
 }
 [HttpPost("{id:guid}/test")] public async Task<IActionResult> Test(Guid id,CancellationToken ct)
 {var x=await db.DataSources.FindAsync([id],ct);if(x is null)return NotFound();using var json=await provider.GetAsync(x,ct);return Content(json.RootElement.GetRawText(),"application/json");}
 [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id,CancellationToken ct){await db.DataSources.Where(x=>x.Id==id).ExecuteDeleteAsync(ct);return NoContent();}
}
