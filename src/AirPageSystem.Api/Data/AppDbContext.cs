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
}
