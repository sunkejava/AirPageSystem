using System.Text.Json;
using AirPageSystem.Api.Contracts;
using AirPageSystem.Api.Data;
using AirPageSystem.Api.Models;
using AirPageSystem.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Controllers;

[ApiController,Route("api/admin")]
public sealed class UsersController(AppDbContext db,CurrentUser current,IPasswordHasher<AppUser> hasher):ControllerBase
{
 [HttpGet("users")] public async Task<IActionResult> Users(CancellationToken ct){await current.DemandAsync("users.manage",ct);var roles=await db.UserRoles.ToListAsync(ct);return Ok((await db.Users.AsNoTracking().ToListAsync(ct)).Select(x=>new{x.Id,x.Username,x.DisplayName,x.IsActive,x.IsAdmin,x.MustChangePassword,roleIds=roles.Where(r=>r.UserId==x.Id).Select(r=>r.RoleId)}));}
 [HttpPost("users")] public async Task<IActionResult> SaveUser(SaveUserRequest r,CancellationToken ct){await current.DemandAsync("users.manage",ct);var name=r.Username.Trim().ToLowerInvariant();if(await db.Users.AnyAsync(x=>x.Username==name,ct))return Conflict("用户名已存在。");if(string.IsNullOrWhiteSpace(r.Password)||r.Password.Length<8)return BadRequest("初始密码至少8位。");var x=new AppUser{Username=name,DisplayName=r.DisplayName.Trim(),PasswordHash="",IsActive=r.IsActive,IsAdmin=r.IsAdmin,MustChangePassword=true};x.PasswordHash=hasher.HashPassword(x,r.Password);db.Users.Add(x);foreach(var role in r.RoleIds.Distinct())db.UserRoles.Add(new(){UserId=x.Id,RoleId=role});await db.SaveChangesAsync(ct);return Ok(new{x.Id});}
 [HttpPut("users/{id:guid}")] public async Task<IActionResult> UpdateUser(Guid id,SaveUserRequest r,CancellationToken ct){await current.DemandAsync("users.manage",ct);var x=await db.Users.FindAsync([id],ct);if(x is null)return NotFound();x.DisplayName=r.DisplayName.Trim();x.IsActive=r.IsActive;x.IsAdmin=r.IsAdmin;if(!string.IsNullOrWhiteSpace(r.Password)){if(r.Password.Length<8)return BadRequest("密码至少8位。");x.PasswordHash=hasher.HashPassword(x,r.Password);x.MustChangePassword=true;}await db.UserRoles.Where(y=>y.UserId==id).ExecuteDeleteAsync(ct);foreach(var role in r.RoleIds.Distinct())db.UserRoles.Add(new(){UserId=id,RoleId=role});await db.SaveChangesAsync(ct);return NoContent();}
 [HttpGet("roles")] public async Task<IActionResult> Roles(CancellationToken ct){await current.DemandAsync("roles.manage",ct);return Ok((await db.Roles.AsNoTracking().ToListAsync(ct)).Select(x=>new{x.Id,x.Name,x.Description,permissions=JsonSerializer.Deserialize<string[]>(x.PermissionsJson)??[]}));}
 [HttpPost("roles")] public async Task<IActionResult> SaveRole(SaveRoleRequest r,CancellationToken ct){await current.DemandAsync("roles.manage",ct);var x=new AppRole{Name=r.Name.Trim(),Description=r.Description??"",PermissionsJson=JsonSerializer.Serialize(r.Permissions.Distinct())};db.Roles.Add(x);await db.SaveChangesAsync(ct);return Ok(x);}
 [HttpPut("roles/{id:guid}")] public async Task<IActionResult> UpdateRole(Guid id,SaveRoleRequest r,CancellationToken ct){await current.DemandAsync("roles.manage",ct);var x=await db.Roles.FindAsync([id],ct);if(x is null)return NotFound();x.Name=r.Name.Trim();x.Description=r.Description??"";x.PermissionsJson=JsonSerializer.Serialize(r.Permissions.Distinct());await db.SaveChangesAsync(ct);return NoContent();}
 [HttpDelete("roles/{id:guid}")] public async Task<IActionResult> DeleteRole(Guid id,CancellationToken ct){await current.DemandAsync("roles.manage",ct);await db.UserRoles.Where(x=>x.RoleId==id).ExecuteDeleteAsync(ct);await db.Roles.Where(x=>x.Id==id).ExecuteDeleteAsync(ct);return NoContent();}
 [HttpGet("permissions")] public IActionResult Permissions()=>Ok(new[]{
  new{code="menu.dashboard",name="总览菜单"},new{code="menu.templates",name="面板模板与设计器菜单"},new{code="menu.sources",name="数据源菜单"},new{code="menu.schedules",name="定时任务菜单"},new{code="menu.devices",name="设备管理菜单"},new{code="menu.history",name="推送历史菜单"},new{code="menu.users",name="用户与角色菜单"},
  new{code="devices.manage",name="管理设备"},new{code="templates.manage",name="管理模板"},new{code="sources.manage",name="管理数据源"},new{code="schedules.manage",name="管理定时任务"},new{code="panels.push",name="推送面板"},new{code="users.manage",name="管理用户"},new{code="roles.manage",name="管理角色"},new{code="data.all",name="查看全部用户数据"}});
}
