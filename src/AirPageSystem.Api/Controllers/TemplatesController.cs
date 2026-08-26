using AirPageSystem.Api.Contracts;
using AirPageSystem.Api.Data;
using AirPageSystem.Api.Models;
using AirPageSystem.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Controllers;
[ApiController, Route("api/templates")]
public sealed class TemplatesController(AppDbContext db,CurrentUser current) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.Templates.Where(x=>x.OwnerUserId==null||x.OwnerUserId==current.Id).OrderBy(x => x.Name).ToListAsync(ct));
    [HttpPost] public async Task<IActionResult> Save(SaveTemplateRequest r, CancellationToken ct)
    {
        await current.DemandAsync("templates.manage",ct);if(r.Type is not("custom" or "designer"))return BadRequest("仅允许创建自定义数据或设计器模板。");var item = new PanelTemplate { Name=r.Name, Type=r.Type, Description=r.Description??"", DataSourceId=r.DataSourceId, SchemaJson=r.SchemaJson,OwnerUserId=current.Id };
        if(r.DataSourceId.HasValue&&!await db.DataSources.AnyAsync(x=>x.Id==r.DataSourceId&&x.OwnerUserId==current.Id,ct))return BadRequest("数据源不存在或无权访问。");
        db.Add(item); await db.SaveChangesAsync(ct); return Ok(item);
    }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, SaveTemplateRequest r, CancellationToken ct)
    {
        await current.DemandAsync("templates.manage",ct);var item=await db.Templates.FirstOrDefaultAsync(x=>x.Id==id&&(x.OwnerUserId==current.Id||x.OwnerUserId==null),ct); if(item is null)return NotFound();
        if(item.IsBuiltIn&&!current.IsAdmin)return Forbid();
        if(!item.IsBuiltIn&&r.DataSourceId.HasValue&&!await db.DataSources.AnyAsync(x=>x.Id==r.DataSourceId&&x.OwnerUserId==current.Id,ct))return BadRequest("数据源不存在或无权访问。");
        item.Name=r.Name;item.Description=r.Description??"";
        if(!item.IsBuiltIn){item.Type=r.Type;item.DataSourceId=r.DataSourceId;item.SchemaJson=r.SchemaJson;}
        item.UpdatedAt=DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);return Ok(item);
    }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id,CancellationToken ct)
    {
        await current.DemandAsync("templates.manage",ct);var item=await db.Templates.FirstOrDefaultAsync(x=>x.Id==id&&x.OwnerUserId==current.Id,ct);if(item is null)return NotFound();if(item.IsBuiltIn)return Conflict("内置模板不可删除。");
        db.Remove(item);await db.SaveChangesAsync(ct);return NoContent();
    }
}
