using System.Security.Claims;
using AirPageSystem.Api.Contracts;
using AirPageSystem.Api.Data;
using AirPageSystem.Api.Models;
using AirPageSystem.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(AppDbContext db, IPasswordHasher<AppUser> hasher, CurrentUser current) : ControllerBase
{
    [AllowAnonymous, HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var name=request.Username.Trim().ToLowerInvariant();
        var user=await db.Users.FirstOrDefaultAsync(x=>x.Username==name,ct);
        if(user is null || !user.IsActive || hasher.VerifyHashedPassword(user,user.PasswordHash,request.Password)==PasswordVerificationResult.Failed)
            return Unauthorized("用户名或密码错误。");
        var identity=new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()),new Claim(ClaimTypes.Name,user.Username),new Claim("display_name",user.DisplayName),new Claim("is_admin",user.IsAdmin?"true":"false")},CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(identity));
        return Ok(await Profile(user,ct));
    }
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete("AirPageSystem.Auth");
        return NoContent();
    }
    [HttpGet("me")] public async Task<IActionResult> Me(CancellationToken ct){var user=await db.Users.FindAsync([current.Id],ct);return user is null?Unauthorized():Ok(await Profile(user,ct));}
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request,CancellationToken ct)
    {
        var user=await db.Users.FindAsync([current.Id],ct);if(user is null)return Unauthorized();
        if(hasher.VerifyHashedPassword(user,user.PasswordHash,request.CurrentPassword)==PasswordVerificationResult.Failed)return BadRequest("当前密码错误。");
        if(request.NewPassword.Length<8)return BadRequest("新密码至少8位。");
        user.PasswordHash=hasher.HashPassword(user,request.NewPassword);user.MustChangePassword=false;await db.SaveChangesAsync(ct);return NoContent();
    }
    private async Task<object> Profile(AppUser user,CancellationToken ct)
    {
        string[] permissions=user.IsAdmin?["*"]:(await (from ur in db.UserRoles where ur.UserId==user.Id join role in db.Roles on ur.RoleId equals role.Id select role.PermissionsJson).ToListAsync(ct)).SelectMany(x=>System.Text.Json.JsonSerializer.Deserialize<string[]>(x)??[]).Distinct().ToArray();
        return new{user.Id,user.Username,user.DisplayName,user.IsAdmin,user.MustChangePassword,permissions,version=typeof(AuthController).Assembly.GetName().Version?.ToString(3)};
    }
}
