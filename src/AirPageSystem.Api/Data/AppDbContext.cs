using AirPageSystem.Api.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<AirPageDevice> Devices => Set<AirPageDevice>();
    public DbSet<PanelTemplate> Templates => Set<PanelTemplate>();
    public DbSet<DataSourceDefinition> DataSources => Set<DataSourceDefinition>();
    public DbSet<ScheduleDefinition> Schedules => Set<ScheduleDefinition>();
    public DbSet<PushRecord> PushRecords => Set<PushRecord>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<AppUserRole> UserRoles => Set<AppUserRole>();
    public DbSet<RetryPolicyDefinition> RetryPolicies => Set<RetryPolicyDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Username).IsUnique();
        modelBuilder.Entity<AppRole>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<AppUserRole>().HasKey(x => new { x.UserId, x.RoleId });
        modelBuilder.Entity<AirPageDevice>().HasIndex(x => new { x.OwnerUserId, x.IsDefault });
        base.OnModelCreating(modelBuilder);
    }
}
