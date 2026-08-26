using AirPageSystem.Api.Contracts;
using AirPageSystem.Api.Data;
using AirPageSystem.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Controllers;

[ApiController, Route("api/devices")]
public sealed class DevicesController(AppDbContext db, AirPageClient client,CurrentUser current) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await db.Devices.Where(x=>x.OwnerUserId==current.Id).Select(x => new { x.Id, x.Name, x.Origin, x.Width, x.Height, x.Mode, x.IsDefault, x.CreatedAt }).ToListAsync(ct));
    [HttpPost] public async Task<IActionResult> Add(AddDeviceRequest request, CancellationToken ct)
    {
        await current.DemandAsync("devices.manage",ct);var (device, _) = client.ParseAndProtect(request.Name, request.DeviceUrl, request.IsDefault);device.OwnerUserId=current.Id;
        if (request.IsDefault) await db.Devices.Where(x=>x.OwnerUserId==current.Id).ExecuteUpdateAsync(x => x.SetProperty(d => d.IsDefault, false), ct);
        if (!await db.Devices.AnyAsync(x=>x.OwnerUserId==current.Id,ct)) device.IsDefault = true;
        db.Devices.Add(device); await db.SaveChangesAsync(ct);
        return Created($"/api/devices/{device.Id}", new { device.Id, device.Name, device.Origin, device.Width, device.Height, device.Mode, device.IsDefault });
    }
    [HttpPut("{id:guid}/default")] public async Task<IActionResult> SetDefault(Guid id, CancellationToken ct)
    {
        await current.DemandAsync("devices.manage",ct);if (!await db.Devices.AnyAsync(x => x.Id == id&&x.OwnerUserId==current.Id, ct)) return NotFound();
        await db.Devices.Where(x=>x.OwnerUserId==current.Id).ExecuteUpdateAsync(x => x.SetProperty(d => d.IsDefault, d => d.Id == id), ct);
        return NoContent();
    }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await current.DemandAsync("devices.manage",ct);await db.Devices.Where(x => x.Id == id&&x.OwnerUserId==current.Id).ExecuteDeleteAsync(ct); return NoContent();
    }
}
