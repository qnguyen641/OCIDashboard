using Microsoft.EntityFrameworkCore;
using OracleWebApplication.Data;
using OracleWebApplication.Models;

namespace OracleWebApplication.Services;

/// <summary>
/// Generates realistic mock OCI cost and data-transfer records
/// for all active tenants. Used when the OCI API is not configured.
/// </summary>
public class MockOciDataService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MockOciDataService> _logger;

    private static readonly string[] Services =
    [
        "COMPUTE", "BLOCK_STORAGE", "OBJECTSTORE", "NETWORK",
        "MYSQL", "ORALB", "WAF", "KMS",
        "STACK_MONITORING", "LOGGING", "TELEMETRY", "OKE_CONTROL_PLANE"
    ];

    private static readonly string[] Regions =
    [
        "us-ashburn-1", "us-phoenix-1", "ap-singapore-1"
    ];

    // Base daily cost per service (USD) — gives realistic proportions
    private static readonly Dictionary<string, decimal> ServiceBaseCost = new()
    {
        ["COMPUTE"]           = 12.50m,
        ["BLOCK_STORAGE"]     = 4.20m,
        ["OBJECTSTORE"]       = 1.80m,
        ["NETWORK"]           = 3.50m,
        ["MYSQL"]             = 8.00m,
        ["ORALB"]             = 2.10m,
        ["WAF"]               = 0.90m,
        ["KMS"]               = 0.45m,
        ["STACK_MONITORING"]  = 1.20m,
        ["LOGGING"]           = 0.60m,
        ["TELEMETRY"]         = 0.35m,
        ["OKE_CONTROL_PLANE"] = 5.00m,
    };

    // How cost is distributed across regions (must sum to ~1.0)
    private static readonly Dictionary<string, decimal> RegionWeight = new()
    {
        ["us-ashburn-1"]     = 0.55m,
        ["us-phoenix-1"]     = 0.30m,
        ["ap-singapore-1"]   = 0.15m,
    };

    public MockOciDataService(IServiceScopeFactory scopeFactory, ILogger<MockOciDataService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task SeedMockDataAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tenants = await db.ClientTenants
            .Where(t => t.IsActive)
            .ToListAsync(ct);

        foreach (var tenant in tenants)
        {
            // Skip if this tenant already has mock data
            if (await db.OciCostRecords.AnyAsync(r => r.ClientTenantId == tenant.Id, ct))
            {
                _logger.LogInformation("Tenant {Id} ({Name}) already has cost data — skipping mock seed.",
                    tenant.Id, tenant.CompanyName);
                continue;
            }

            await GenerateCostRecordsAsync(db, tenant, ct);
            await GenerateTransferRecordsAsync(db, tenant, ct);

            _logger.LogInformation("Seeded mock OCI data for tenant {Id} ({Name})",
                tenant.Id, tenant.CompanyName);
        }
    }

    private static async Task GenerateCostRecordsAsync(
        ApplicationDbContext db, ClientTenant tenant, CancellationToken ct)
    {
        var rng = new Random(tenant.Id * 31); // deterministic per tenant
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(-90);

        var records = new List<OciCostRecord>();

        for (var date = start; date < today; date = date.AddDays(1))
        {
            // Slight weekly pattern: weekends are ~70 % of weekday cost
            var dayOfWeek = date.DayOfWeek;
            var weekendFactor = (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
                ? 0.70m : 1.0m;

            // Gradual upward trend over 90 days (simulates growth)
            var dayIndex = date.DayNumber - start.DayNumber;
            var trendFactor = 1.0m + (dayIndex * 0.002m);

            foreach (var service in Services)
            {
                var baseCost = ServiceBaseCost[service];

                foreach (var region in Regions)
                {
                    var regionFactor = RegionWeight[region];

                    // ±15 % random daily variation
                    var noise = 0.85m + (decimal)(rng.NextDouble() * 0.30);

                    var cost = Math.Round(
                        baseCost * regionFactor * weekendFactor * trendFactor * noise, 6);

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

        db.OciCostRecords.AddRange(records);
        await db.SaveChangesAsync(ct);
    }

    private static async Task GenerateTransferRecordsAsync(
        ApplicationDbContext db, ClientTenant tenant, CancellationToken ct)
    {
        var rng = new Random(tenant.Id * 17);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(-90);

        var records = new List<OciDataTransferRecord>();

        for (var date = start; date < today; date = date.AddDays(1))
        {
            var dayOfWeek = date.DayOfWeek;
            var weekendFactor = (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
                ? 0.60m : 1.0m;

            var dayIndex = date.DayNumber - start.DayNumber;
            var trendFactor = 1.0m + (dayIndex * 0.003m);

            // Base ~25 GB/day outbound with ±20 % noise
            var noise = 0.80m + (decimal)(rng.NextDouble() * 0.40);
            var gb = Math.Round(25m * weekendFactor * trendFactor * noise, 6);

            records.Add(new OciDataTransferRecord
            {
                ClientTenantId = tenant.Id,
                UsageDate = date,
                OutboundGb = gb
            });
        }

        db.OciDataTransferRecords.AddRange(records);
        await db.SaveChangesAsync(ct);
    }
}
