namespace AirPageSystem.Api.Contracts;

public sealed record AddDeviceRequest(string Name, string DeviceUrl, bool IsDefault = false);
public sealed record ExecutePanelRequest(Guid TemplateId, Guid? DeviceId, bool Push = true);
public sealed record SaveTemplateRequest(string Name, string Type, string? Description, Guid? DataSourceId, string? SchemaJson);
public sealed record SaveDataSourceRequest(string Name, string Method, string Url, string? HeadersJson, string? Body, bool Enabled);
public sealed record SaveScheduleRequest(string Name, Guid TemplateId, Guid DeviceId, string Cron, string TimeZoneId, bool Enabled);

