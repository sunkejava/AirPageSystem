using AirPageSystem.Api.Data;
using AirPageSystem.Api.Services;
using Microsoft.EntityFrameworkCore;

System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=data/airpage.db"));
builder.Services.AddDataProtection().PersistKeysToDbContext<AppDbContext>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<SystemStatusProvider>();
builder.Services.AddSingleton<MarketDataProvider>();
builder.Services.AddSingleton<CustomJsonDataProvider>();
builder.Services.AddSingleton<PanelRenderer>();
builder.Services.AddScoped<AirPageClient>();
builder.Services.AddScoped<PanelExecutionService>();
builder.Services.AddHostedService<ScheduleWorker>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "data"));
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedData.InitializeAsync(db);
}
app.UseDefaultFiles();
app.UseStaticFiles();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();
