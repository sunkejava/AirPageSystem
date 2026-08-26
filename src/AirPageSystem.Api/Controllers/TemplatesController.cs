using AirPageSystem.Api.Contracts;
using AirPageSystem.Api.Data;
using AirPageSystem.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Controllers;
[ApiController, Route("api/templates")]
public sealed class TemplatesController(AppDbContext db) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.Templates.OrderBy(x => x.Name).ToListAsync(ct));
    [HttpPost] public async Task<IActionResult> Save(SaveTemplateRequest r, CancellationToken ct)
    {
        var item = new PanelTemplate { Name=r.Name, Type=r.Type, Description=r.Description??"", DataSourceId=r.DataSourceId, SchemaJson=r.SchemaJson };
        db.Add(item); await db.SaveChangesAsync(ct); return Ok(item);
    }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, SaveTemplateRequest r, CancellationToken ct)
    {
        var item=await db.Templates.FindAsync([id],ct); if(item is null)return NotFound();
        item.Name=r.Name;item.Description=r.Description??"";
        if(!item.IsBuiltIn){item.Type=r.Type;item.DataSourceId=r.DataSourceId;item.SchemaJson=r.SchemaJson;}
        item.UpdatedAt=DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);return Ok(item);
    }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id,CancellationToken ct)
    {
        var item=await db.Templates.FindAsync([id],ct);if(item is null)return NotFound();if(item.IsBuiltIn)return Conflict("内置模板不可删除。");
        db.Remove(item);await db.SaveChangesAsync(ct);return NoContent();
    }
}
