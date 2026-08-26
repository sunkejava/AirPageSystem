using System.Security.Claims;
using System.Text.Json;
using AirPageSystem.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Services;

public sealed class CurrentUser(IHttpContextAccessor accessor, AppDbContext db)
{
    public Guid Id => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("用户未登录。");
    public bool IsAdmin => string.Equals(accessor.HttpContext?.User.FindFirstValue("is_admin"), "true", StringComparison.OrdinalIgnoreCase);
    public async Task<HashSet<string>> PermissionsAsync(CancellationToken ct)
    {
        if (IsAdmin) return new(StringComparer.OrdinalIgnoreCase) { "*" };
        var json = await (from ur in db.UserRoles where ur.UserId == Id join role in db.Roles on ur.RoleId equals role.Id select role.PermissionsJson).ToListAsync(ct);
        return json.SelectMany(x => JsonSerializer.Deserialize<string[]>(x) ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
    public async Task DemandAsync(string permission, CancellationToken ct)
    {
        var permissions = await PermissionsAsync(ct);
        if (!permissions.Contains("*") && !permissions.Contains(permission)) throw new UnauthorizedAccessException($"缺少权限：{permission}");
    }
    public async Task<bool> CanReadAllAsync(CancellationToken ct) => IsAdmin || (await PermissionsAsync(ct)).Contains("data.all");
}

public static class OwnershipQuery
{
    public static async Task<IQueryable<T>> VisibleToAsync<T>(this IQueryable<T> source, CurrentUser user, CancellationToken ct) where T : class
    {
        if (await user.CanReadAllAsync(ct)) return source;
        var owner = typeof(T).GetProperty("OwnerUserId") ?? throw new InvalidOperationException($"{typeof(T).Name}不支持数据权限。");
        return source.Where(x => EF.Property<Guid?>(x, owner.Name) == user.Id || EF.Property<Guid?>(x, owner.Name) == null);
    }
}
