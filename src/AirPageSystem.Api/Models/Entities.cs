using System.ComponentModel.DataAnnotations;

namespace AirPageSystem.Api.Models;

public sealed class AirPageDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(80)] public required string Name { get; set; }
    [MaxLength(200)] public required string Origin { get; set; }
    public required string ProtectedDeviceId { get; set; }
    public int Width { get; set; } = 528;
    public int Height { get; set; } = 792;
    [MaxLength(20)] public string Mode { get; set; } = "gray4";
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PanelTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(100)] public required string Name { get; set; }
    [MaxLength(40)] public required string Type { get; set; }
    [MaxLength(300)] public string Description { get; set; } = "";
    public string? SchemaJson { get; set; }
    public Guid? DataSourceId { get; set; }
    public bool IsBuiltIn { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DataSourceDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(100)] public required string Name { get; set; }
    [MaxLength(20)] public string Method { get; set; } = "GET";
    [MaxLength(2048)] public required string Url { get; set; }
    public string? ProtectedHeadersJson { get; set; }
    public string? Body { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class ScheduleDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(100)] public required string Name { get; set; }
    public Guid TemplateId { get; set; }
    public Guid DeviceId { get; set; }
    [MaxLength(100)] public required string Cron { get; set; }
    [MaxLength(80)] public string TimeZoneId { get; set; } = "Asia/Shanghai";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public string? LastResult { get; set; }
}

public sealed class PushRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemplateId { get; set; }
    public Guid DeviceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool UploadSucceeded { get; set; }
    public bool Refreshed { get; set; }
    public int BmpBytes { get; set; }
    public long DurationMs { get; set; }
    [MaxLength(500)] public string Message { get; set; } = "";
    [MaxLength(500)] public string? PreviewPath { get; set; }
}
