using System.Net;
using System.Text.Json;
using AirPageSystem.Api.Models;
using Microsoft.AspNetCore.DataProtection;

namespace AirPageSystem.Api.Services;

public sealed class AirPageClient(IHttpClientFactory clients, IDataProtectionProvider protection, ILogger<AirPageClient> logger)
{
    private readonly IDataProtector _protector = protection.CreateProtector("AirPageSystem.DeviceId.v1");
    private static readonly HashSet<string> TrustedHosts = new(StringComparer.OrdinalIgnoreCase)
        { "airpage.crossmux.cn", "airpage.crossmux.com", "airpage.yunhug.com" };

    public (AirPageDevice Device, string DeviceId) ParseAndProtect(string name, string rawUrl, bool isDefault)
    {
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !TrustedHosts.Contains(uri.Host))
            throw new ArgumentException("仅允许受信任的 AirPage HTTPS 地址。");
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        var id = query["id"].ToString();
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[A-Za-z0-9_-]{16}$"))
            throw new ArgumentException("设备ID格式无效。");
        if (!int.TryParse(query["w"], out var width) || !int.TryParse(query["h"], out var height) || width <= 0 || height <= 0)
            throw new ArgumentException("设备尺寸无效。");
        if (!string.Equals(query["mode"], "gray4", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("仅支持 gray4 模式。");
        var device = new AirPageDevice
        {
            Name = name.Trim(), Origin = uri.GetLeftPart(UriPartial.Authority), ProtectedDeviceId = _protector.Protect(id),
            Width = width, Height = height, Mode = "gray4", IsDefault = isDefault
        };
        return (device, id);
    }

    public async Task<PushResult> PushAsync(AirPageDevice device, byte[] bmp, CancellationToken ct)
    {
        if (bmp.Length > 512 * 1024) return new(false, false, $"BMP超出512 KiB限制：{bmp.Length}字节");
        var id = _protector.Unprotect(device.ProtectedDeviceId);
        var http = clients.CreateClient();
        using var content = new ByteArrayContent(bmp);
        content.Headers.ContentType = new("image/bmp");
        var endpoint = $"{device.Origin.TrimEnd('/')}/api/device/{Uri.EscapeDataString(id)}/push";
        using var response = await http.PostAsync(endpoint, content, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            using var fallback = new ByteArrayContent(bmp);
            fallback.Headers.ContentType = new("image/bmp");
            using var fallbackResponse = await http.PostAsync(
                $"{device.Origin.TrimEnd('/')}/api/device/{Uri.EscapeDataString(id)}/image", fallback, ct);
            return new(fallbackResponse.IsSuccessStatusCode, false,
                fallbackResponse.IsSuccessStatusCode ? "已上传，需手动刷新" : $"上传失败：HTTP {(int)fallbackResponse.StatusCode}");
        }
        if (!response.IsSuccessStatusCode) return new(false, false, $"上传失败：HTTP {(int)response.StatusCode}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var ok = json.TryGetProperty("ok", out var okNode) && okNode.GetBoolean();
        var refreshed = json.TryGetProperty("refreshed", out var refreshNode) && refreshNode.GetBoolean();
        logger.LogInformation("AirPage push completed. Success={Success}, Refreshed={Refreshed}, Bytes={Bytes}", ok, refreshed, bmp.Length);
        return new(ok, refreshed, ok ? (refreshed ? "上传成功并请求自动刷新" : "上传成功，请按设备下键刷新") : "服务端未确认上传");
    }
}
public sealed record PushResult(bool Succeeded, bool Refreshed, string Message);
