namespace AirPageSystem.Api.Contracts;

public sealed record AddDeviceRequest(string Name, string DeviceUrl, bool IsDefault = false);
public sealed record ExecutePanelRequest(Guid TemplateId, Guid? DeviceId, bool Push = true, Guid? RetryPolicyId = null);
public sealed record SaveTemplateRequest(string Name, string Type, string? Description, Guid? DataSourceId, string? SchemaJson);
public sealed record SaveDataSourceRequest(string Name, string Method, string Url, string? HeadersJson, string? Body, bool Enabled);
public sealed record SaveScheduleRequest(string Name, Guid TemplateId, Guid DeviceId, string Cron, string TimeZoneId, bool Enabled, Guid? RetryPolicyId = null);
public sealed record LoginRequest(string Username, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record SaveUserRequest(string Username, string DisplayName, string? Password, bool IsActive, bool IsAdmin, Guid[] RoleIds);
public sealed record SaveRoleRequest(string Name, string? Description, string[] Permissions);
public sealed record SaveRetryPolicyRequest(string Name, int MaxAttempts, int InitialDelayMs, double BackoffFactor,
    int MaxDelayMs, bool RetryPreview, bool RetryPush, bool IsDefault);
