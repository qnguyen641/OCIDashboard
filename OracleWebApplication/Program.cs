using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using OracleWebApplication.Data;
using OracleWebApplication.Models;
using OracleWebApplication.Services;

var builder = WebApplication.CreateBuilder(args);

// Render sets PORT env var — bind to it
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// EF Core — auto-detect PostgreSQL (Supabase) vs MySQL (local dev)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var usePostgres = connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
               || connectionString.Contains("postgresql", StringComparison.OrdinalIgnoreCase)
               || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATABASE_URL"));

// If DATABASE_URL env var is set (Render/Supabase), use it
var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(dbUrl))
{
    connectionString = dbUrl;
    usePostgres = true;
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (usePostgres)
        options.UseNpgsql(connectionString);
    else
        options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)));
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
