using AirPageSystem.Api.Contracts;
using AirPageSystem.Api.Data;
using AirPageSystem.Api.Models;
using AirPageSystem.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Controllers;
[ApiController,Route("api/retry-policies")]
public sealed class RetryPoliciesController(AppDbContext db,CurrentUser current):ControllerBase
{
 [HttpGet] public async Task<IActionResult> List(CancellationToken ct)=>Ok(await db.RetryPolicies.Where(x=>x.OwnerUserId==current.Id).OrderByDescending(x=>x.IsDefault).ToListAsync(ct));
 [HttpPost] public async Task<IActionResult> Save(SaveRetryPolicyRequest r,CancellationToken ct){await current.DemandAsync("panels.push",ct);var error=Validate(r);if(error is not null)return BadRequest(error);if(r.IsDefault)await db.RetryPolicies.Where(x=>x.OwnerUserId==current.Id).ExecuteUpdateAsync(x=>x.SetProperty(y=>y.IsDefault,false),ct);var x=new RetryPolicyDefinition{OwnerUserId=current.Id,Name=r.Name,MaxAttempts=r.MaxAttempts,InitialDelayMs=r.InitialDelayMs,BackoffFactor=r.BackoffFactor,MaxDelayMs=r.MaxDelayMs,RetryPreview=r.RetryPreview,RetryPush=r.RetryPush,IsDefault=r.IsDefault};db.Add(x);await db.SaveChangesAsync(ct);return Ok(x);}
 [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id,SaveRetryPolicyRequest r,CancellationToken ct){var x=await db.RetryPolicies.FirstOrDefaultAsync(x=>x.Id==id&&x.OwnerUserId==current.Id,ct);if(x is null)return NotFound();var error=Validate(r);if(error is not null)return BadRequest(error);if(r.IsDefault)await db.RetryPolicies.Where(y=>y.OwnerUserId==current.Id).ExecuteUpdateAsync(y=>y.SetProperty(z=>z.IsDefault,false),ct);x.Name=r.Name;x.MaxAttempts=r.MaxAttempts;x.InitialDelayMs=r.InitialDelayMs;x.BackoffFactor=r.BackoffFactor;x.MaxDelayMs=r.MaxDelayMs;x.RetryPreview=r.RetryPreview;x.RetryPush=r.RetryPush;x.IsDefault=r.IsDefault;await db.SaveChangesAsync(ct);return Ok(x);}
 [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id,CancellationToken ct){await db.RetryPolicies.Where(x=>x.Id==id&&x.OwnerUserId==current.Id).ExecuteDeleteAsync(ct);return NoContent();}
 private static string? Validate(SaveRetryPolicyRequest r)=>r.MaxAttempts is<1 or>10?"最大尝试次数范围为1-10。":r.InitialDelayMs is<0 or>60000?"初始延迟范围为0-60000ms。":r.BackoffFactor is<1 or>10?"退避倍数范围为1-10。":r.MaxDelayMs is<0 or>300000?"最大延迟范围为0-300000ms。":null;
}
