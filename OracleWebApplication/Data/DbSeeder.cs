using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OracleWebApplication.Models;

namespace OracleWebApplication.Data;

public static class DbSeeder
{
    // Services matching the OCI demo dashboard
    private static readonly string[] Services =
    [
        "COMPUTE", "BLOCK_STORAGE", "OBJECTSTORE", "NETWORK",
        "MYSQL", "ORALB", "WAF", "KMS",
        "STACK_MONITORING", "LOGGING", "TELEMETRY", "OKE_CONTROL_PLANE"
    ];

    // Regions matching the screenshot
    private static readonly string[] Regions =
    [
        "ap-seoul-1", "ap-singapore-1", "eu-frankfurt-1", "sa-saopaulo-1",
        "us-sanjose-1", "ap-tokyo-1", "us-phoenix-1", "ap-sydney-1",
        "us-ashburn-1", "me-dubai-1"
    ];

    // Base daily cost per service (USD) – proportions match the screenshot tables
    private static readonly Dictionary<string, decimal> ServiceBaseCost = new()
    {
        ["COMPUTE"]           = 38.00m,
        ["BLOCK_STORAGE"]     = 5.50m,
        ["OBJECTSTORE"]       = 1.60m,
        ["NETWORK"]           = 0.12m,
        ["MYSQL"]             = 2.40m,
        ["ORALB"]             = 5.80m,
        ["WAF"]               = 0.00m,
        ["KMS"]               = 0.00m,
        ["STACK_MONITORING"]  = 4.80m,
        ["LOGGING"]           = 3.20m,
        ["TELEMETRY"]         = 0.10m,
        ["OKE_CONTROL_PLANE"] = 0.00m,
    };

    // Regional weight distribution matching the pie chart
    private static readonly Dictionary<string, decimal> RegionWeight = new()
    {
        ["ap-seoul-1"]      = 0.27m,
        ["ap-singapore-1"]  = 0.10m,
        ["eu-frankfurt-1"]  = 0.30m,
        ["sa-saopaulo-1"]   = 0.02m,
        ["us-sanjose-1"]    = 0.02m,
        ["ap-tokyo-1"]      = 0.18m,
        ["us-phoenix-1"]    = 0.005m,
        ["ap-sydney-1"]     = 0.005m,
        ["us-ashburn-1"]    = 0.08m,
        ["me-dubai-1"]      = 0.005m,
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (env.IsDevelopment())
        {
            // Local dev: drop and recreate so schema changes apply cleanly.
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();
        }
        else
        {
            // Production (Render/Supabase): create tables if they don't exist.
            // Uses EnsureCreatedAsync because existing EF migrations target MySQL.
            await db.Database.EnsureCreatedAsync();
        }

        // Seed Admin role
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        // Seed demo tenant (Q_Demo Cost Analysis)
        var tenant = await db.ClientTenants.FirstOrDefaultAsync(t => t.CompanyName == "Q_Demo Cost Analysis");
        if (tenant is null)
        {
            tenant = new ClientTenant
            {
                CompanyName = "Q_Demo Cost Analysis",
                OciTenancyOcid = "ocid1.tenancy.oc1..demo",
                CompartmentOcid = "ocid1.compartment.oc1..demo",
                Description = "Demo tenant for OCI cost analysis dashboard"
            };
            db.ClientTenants.Add(tenant);
            await db.SaveChangesAsync();
        }

        // Seed demo user: quangnguyen642001@gmail.com
        const string demoEmail = "quangnguyen642001@gmail.com";
        const string demoPassword = "Admin@1234";

        if (await userManager.FindByEmailAsync(demoEmail) is null)
        {
            var demoUser = new ApplicationUser
            {
                UserName = demoEmail,
                Email = demoEmail,
                DisplayName = "Quang Nguyen",
                ClientTenantId = tenant.Id,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(demoUser, demoPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(demoUser, "Admin");
            }
        }

        // Seed 14 months of mock cost records (2025-01 through 2026-02)
        if (!await db.OciCostRecords.AnyAsync(r => r.ClientTenantId == tenant.Id))
        {
            await SeedCostRecordsAsync(db, tenant);
        }

        // Seed outbound data transfer records (last ~20 days of Feb 2026)
        if (!await db.OciDataTransferRecords.AnyAsync(r => r.ClientTenantId == tenant.Id))
        {
            await SeedTransferRecordsAsync(db, tenant);
        }
    }

    private static async Task SeedCostRecordsAsync(ApplicationDbContext db, ClientTenant tenant)
    {
        var rng = new Random(42);
        var records = new List<OciCostRecord>();

        // Monthly totals from the screenshot (approximate USD)
        var monthlyTargets = new Dictionary<string, decimal>
        {
            ["2025-01"] = 6607m, ["2025-02"] = 6264m, ["2025-03"] = 8828m,
            ["2025-04"] = 7862m, ["2025-05"] = 8647m, ["2025-06"] = 8124m,
            ["2025-07"] = 7809m, ["2025-08"] = 7500m, ["2025-09"] = 6283m,
            ["2025-10"] = 5870m, ["2025-11"] = 5172m, ["2025-12"] = 7215m,
            ["2026-01"] = 8546m, ["2026-02"] = 5540m,
        };

        foreach (var (monthKey, monthTotal) in monthlyTargets)
        {
            var year = int.Parse(monthKey[..4]);
            var month = int.Parse(monthKey[5..]);
            var daysInMonth = DateTime.DaysInMonth(year, month);

            // For Feb 2026, only generate up to the 28th
            var lastDay = (year == 2026 && month == 2) ? 28 : daysInMonth;

            // Target daily total = monthTotal / days
            var dailyBase = monthTotal / lastDay;

            for (var day = 1; day <= lastDay; day++)
            {
                var date = new DateOnly(year, month, day);
                var weekendFactor = (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    ? 0.75m : 1.0m;

                foreach (var service in Services)
                {
                    var svcBase = ServiceBaseCost[service];
                    if (svcBase == 0m) continue;

                    foreach (var region in Regions)
                    {
                        var rw = RegionWeight[region];
                        if (rw < 0.01m && rng.NextDouble() > 0.3) continue;

                        var noise = 0.80m + (decimal)(rng.NextDouble() * 0.40);
                        var cost = Math.Round(svcBase * rw * weekendFactor * noise, 6);
                        if (cost < 0.001m) continue;

                        records.Add(new OciCostRecord
                        {
                            ClientTenantId = tenant.Id,
                            UsageDate = date,
                            Service = service,
                            Region = region,
                            Cost = cost,
                            Currency = "USD"
                        });
                    }
                }
            }
        }

        db.OciCostRecords.AddRange(records);
        await db.SaveChangesAsync();
    }

    private static async Task SeedTransferRecordsAsync(ApplicationDbContext db, ClientTenant tenant)
    {
        var rng = new Random(17);
        var records = new List<OciDataTransferRecord>();

        // Generate 2026-02-01 through 2026-02-19 matching the area chart shape
        // Peak around Feb 2-3 (~1.8 GB), then drop, small bump mid-month
        decimal[] dailyGb =
        [
            0.8m, 1.8m, 2.0m, 0.5m, 0.3m, 0.2m, 0.6m, 0.4m, 0.3m, 0.2m,
            0.3m, 0.2m, 0.15m, 0.1m, 0.1m, 0.08m, 0.05m, 0.03m, 0.02m
        ];

        for (var i = 0; i < dailyGb.Length; i++)
        {
            var date = new DateOnly(2026, 2, i + 1);
            var noise = 0.90m + (decimal)(rng.NextDouble() * 0.20);
            records.Add(new OciDataTransferRecord
            {
                ClientTenantId = tenant.Id,
                UsageDate = date,
                OutboundGb = Math.Round(dailyGb[i] * noise, 6)
            });
        }

        db.OciDataTransferRecords.AddRange(records);
        await db.SaveChangesAsync();
    }
}
