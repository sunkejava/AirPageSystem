using System.Net;
using System.Text;
using System.Text.Json;
using AirPageSystem.Api.Models;
using Microsoft.AspNetCore.DataProtection;

namespace AirPageSystem.Api.Services;

public sealed class CustomJsonDataProvider(IHttpClientFactory clients, IConfiguration configuration, IDataProtectionProvider protection)
{
    private readonly IDataProtector _protector = protection.CreateProtector("AirPageSystem.DataSourceHeaders.v1");
    public async Task<JsonDocument> GetAsync(DataSourceDefinition source, CancellationToken ct)
    {
        var uri = new Uri(source.Url);
        if (uri.Scheme is not ("http" or "https")) throw new InvalidOperationException("仅支持HTTP/HTTPS数据源。");
        var allowPrivate = configuration.GetValue("DataSources:AllowPrivateNetworks", false);
        if (!allowPrivate && await ResolvesToPrivateAsync(uri.Host)) throw new InvalidOperationException("默认禁止访问内网地址，可在配置中显式开启。");
        var request = new HttpRequestMessage(new HttpMethod(source.Method), uri);
        if (!string.IsNullOrWhiteSpace(source.Body)) request.Content = new StringContent(source.Body, Encoding.UTF8, "application/json");
        if (!string.IsNullOrWhiteSpace(source.ProtectedHeadersJson))
            foreach (var kv in JsonSerializer.Deserialize<Dictionary<string, string>>(_protector.Unprotect(source.ProtectedHeadersJson)) ?? [])
                request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        var http = clients.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(configuration.GetValue("DataSources:RequestTimeoutSeconds", 15));
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
    }
    private static async Task<bool> ResolvesToPrivateAsync(string host) =>
        (await Dns.GetHostAddressesAsync(host)).Any(x => x.IsIPv6LinkLocal || x.IsIPv6SiteLocal ||
            x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
            (x.GetAddressBytes()[0] == 10 || x.GetAddressBytes()[0] == 127 ||
             x.GetAddressBytes()[0] == 192 && x.GetAddressBytes()[1] == 168 ||
             x.GetAddressBytes()[0] == 172 && x.GetAddressBytes()[1] is >= 16 and <= 31));
}
