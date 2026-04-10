using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using OracleWebApplication.Data;
using OracleWebApplication.Models;
using OracleWebApplication.Services;

var builder = WebApplication.CreateBuilder(args);

// Load .env file if it exists (local dev convenience)
// Walk up from the app's base directory to find .env at the solution root
string? envFile = null;
var searchDir = new DirectoryInfo(AppContext.BaseDirectory);
while (searchDir is not null)
{
    var candidate = Path.Combine(searchDir.FullName, ".env");
    if (File.Exists(candidate)) { envFile = candidate; break; }
    searchDir = searchDir.Parent;
}
if (envFile is not null)
{
    foreach (var line in File.ReadAllLines(envFile))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
        var idx = trimmed.IndexOf('=');
        if (idx <= 0) continue;
        var key = trimmed[..idx].Trim();
        var value = trimmed[(idx + 1)..].Trim();
        Environment.SetEnvironmentVariable(key, value);
    }
    // Reload configuration so env vars take effect
    builder.Configuration.AddEnvironmentVariables();
}

// Render sets PORT env var — bind to it
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// EF Core — auto-detect PostgreSQL (Supabase) vs MySQL (local dev)
// DATABASE_URL env var takes priority (set on Render); falls back to appsettings
var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string connectionString;
bool usePostgres;

if (!string.IsNullOrEmpty(dbUrl))
{
    // Supabase/Render provide a URI like postgresql://user:pass@host:port/db
    // Npgsql needs a standard connection string: Host=...;Port=...;Database=...;Username=...;Password=...
    if (dbUrl.StartsWith("postgresql://") || dbUrl.StartsWith("postgres://"))
    {
        var uri = new Uri(dbUrl);
        var userInfo = uri.UserInfo.Split(':');
        connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')}"
                         + $";Username={Uri.UnescapeDataString(userInfo[0])}"
                         + $";Password={Uri.UnescapeDataString(userInfo[1])}"
                         + ";SSL Mode=Require;Trust Server Certificate=true";
    }
    else
    {
        connectionString = dbUrl;
    }
    usePostgres = true;
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    usePostgres = connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
              || connectionString.Contains("postgresql", StringComparison.OrdinalIgnoreCase);
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (usePostgres)
        options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure());
    else
        options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)),
            mysql => mysql.EnableRetryOnFailure());
});

// ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Session timeout 30 min
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Localisation
builder.Services.AddSingleton<OracleWebApplication.Services.JsonLocalizer>();
builder.Services.AddRazorPages();
builder.Services.AddScoped<AuditService>();

// SMTP email sender for password reset
builder.Services.Configure<OracleWebApplication.Services.SmtpSettings>(
    builder.Configuration.GetSection(OracleWebApplication.Services.SmtpSettings.SectionName));
builder.Services.AddTransient<IEmailSender<ApplicationUser>, OracleWebApplication.Services.SmtpEmailSender>();

// OCI API integration
builder.Services.Configure<OracleWebApplication.Models.OciApiSettings>(
    builder.Configuration.GetSection(OracleWebApplication.Models.OciApiSettings.SectionName));
builder.Services.AddSingleton<OciRequestSigner>();
builder.Services.AddHttpClient<OciUsageApiService>();
builder.Services.AddScoped<MockOciDataService>();
builder.Services.AddHostedService<OciDataRefreshService>();

var app = builder.Build();

// Seed database (runs migrations + creates default tenant & admin account)
await DbSeeder.SeedAsync(app.Services);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Render handles HTTPS at the proxy level; only redirect locally
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRouting();

// Localisation middleware — cookie-based culture selection
var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("zh-CN") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.MapRazorPages();

app.Run();
