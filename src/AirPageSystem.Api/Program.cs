using AirPageSystem.Api.Data;
using AirPageSystem.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=data/airpage.db"));
builder.Services.AddDataProtection().PersistKeysToDbContext<AppDbContext>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<AirPageSystem.Api.Models.AppUser>, Microsoft.AspNetCore.Identity.PasswordHasher<AirPageSystem.Api.Models.AppUser>>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "AirPageSystem.Auth";
    options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Strict; options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(12); options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = c => { c.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});
builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddHttpClient("external", client => client.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddSingleton<SystemStatusProvider>();
builder.Services.AddSingleton<ThreeXUiMonitorProvider>();
builder.Services.AddSingleton<MarketDataProvider>();
builder.Services.AddSingleton<CustomJsonDataProvider>();
builder.Services.AddSingleton<PanelRenderer>();
builder.Services.AddSingleton<RetryExecutor>();
builder.Services.AddScoped<AirPageClient>();
builder.Services.AddScoped<PanelExecutionService>();
builder.Services.AddHostedService<ScheduleWorker>();
builder.Services.AddControllers();

var app = builder.Build();
var version=typeof(Program).Assembly.GetName().Version?.ToString(3)??"unknown";
app.Logger.LogInformation("AirPageSystem v{Version} starting. Environment={Environment}",version,app.Environment.EnvironmentName);
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "data"));
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DatabaseUpgrade.ApplyAsync(db);
    await SeedData.InitializeAsync(db, scope.ServiceProvider, builder.Configuration);
}
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/version", () => Results.Ok(new { version })).AllowAnonymous();
// Vue routes are client-side routes. The fallback must remain public so a direct
// navigation or browser refresh can load the login shell before authentication.
app.MapFallbackToFile("index.html").AllowAnonymous();
app.Run();
