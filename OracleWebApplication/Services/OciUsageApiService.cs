using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OracleWebApplication.Data;
using OracleWebApplication.Models;

namespace OracleWebApplication.Services;

/// <summary>
/// Fetches cost and usage data from the OCI Usage API and caches it locally.
/// </summary>
public class OciUsageApiService
{
    private readonly HttpClient _http;
    private readonly OciRequestSigner _signer;
    private readonly OciApiSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OciUsageApiService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OciUsageApiService(
        HttpClient http,
        OciRequestSigner signer,
        IOptions<OciApiSettings> settings,
        IServiceScopeFactory scopeFactory,
        ILogger<OciUsageApiService> logger)
    {
        _http = http;
        _signer = signer;
        _settings = settings.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private string BaseUrl =>
        $"https://usageapi.{_settings.Region}.oci.oraclecloud.com/20200107";

    /// <summary>
    /// Refreshes cached cost data for all active tenants.
    /// Pulls the last 90 days of daily cost data from OCI.
    /// </summary>
    public async Task RefreshAllTenantsAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tenants = await db.ClientTenants
            .Where(t => t.IsActive && t.CompartmentOcid != null && t.CompartmentOcid != "")
            .ToListAsync(ct);

        foreach (var tenant in tenants)
        {
            try
            {
                await RefreshTenantCostDataAsync(db, tenant, ct);
                await RefreshTenantTransferDataAsync(db, tenant, ct);
                _logger.LogInformation("Refreshed OCI data for tenant {TenantId} ({Company})",
                    tenant.Id, tenant.CompanyName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh OCI data for tenant {TenantId} ({Company})",
                    tenant.Id, tenant.CompanyName);
            }
        }
    }

    private async Task RefreshTenantCostDataAsync(
        ApplicationDbContext db, ClientTenant tenant, CancellationToken ct)
    {
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-90);

        var requestBody = new
        {
            tenantId = _settings.TenancyOcid,
            timeUsageStarted = start.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            timeUsageEnded = end.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            granularity = "DAILY",
            queryType = "COST",
            groupBy = new[] { "service", "region" },
            compartmentDepth = 1,
            filter = new
            {
                @operator = "AND",
                dimensions = new[]
                {
                    new { key = "compartmentId", value = tenant.CompartmentOcid }
                }
            }
        };

        var items = await PostUsageQueryAsync(requestBody, ct);
        if (items is null) return;

        // Remove stale records for this tenant in the date range, then insert fresh ones
        var startDate = DateOnly.FromDateTime(start);
        var endDate = DateOnly.FromDateTime(end);

        var stale = await db.OciCostRecords
            .Where(r => r.ClientTenantId == tenant.Id
                        && r.UsageDate >= startDate
                        && r.UsageDate <= endDate)
            .ToListAsync(ct);
        db.OciCostRecords.RemoveRange(stale);

        foreach (var item in items)
        {
            if (!item.TryGetProperty("timeUsageStarted", out var timeProp)) continue;
            if (!DateTime.TryParse(timeProp.GetString(), out var usageTime)) continue;

            var service = GetDimension(item, "service");
            var region = GetDimension(item, "region");
            var cost = GetCost(item);

            db.OciCostRecords.Add(new OciCostRecord
            {
                ClientTenantId = tenant.Id,
                UsageDate = DateOnly.FromDateTime(usageTime),
                Service = service,
                Region = region,
                Cost = cost,
                Currency = "USD"
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task RefreshTenantTransferDataAsync(
        ApplicationDbContext db, ClientTenant tenant, CancellationToken ct)
    {
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-90);

        var requestBody = new
        {
            tenantId = _settings.TenancyOcid,
            timeUsageStarted = start.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            timeUsageEnded = end.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            granularity = "DAILY",
            queryType = "USAGE",
            groupBy = new[] { "service" },
            compartmentDepth = 1,
            filter = new
            {
                @operator = "AND",
                dimensions = new[]
                {
                    new { key = "compartmentId", value = tenant.CompartmentOcid },
                    new { key = "service", value = "NETWORK" }
                }
            }
        };

        var items = await PostUsageQueryAsync(requestBody, ct);
        if (items is null) return;

        var startDate = DateOnly.FromDateTime(start);
        var endDate = DateOnly.FromDateTime(end);

        var stale = await db.OciDataTransferRecords
            .Where(r => r.ClientTenantId == tenant.Id
                        && r.UsageDate >= startDate
                        && r.UsageDate <= endDate)
            .ToListAsync(ct);
        db.OciDataTransferRecords.RemoveRange(stale);

        foreach (var item in items)
        {
            if (!item.TryGetProperty("timeUsageStarted", out var timeProp)) continue;
            if (!DateTime.TryParse(timeProp.GetString(), out var usageTime)) continue;

            var quantity = GetQuantity(item);

            db.OciDataTransferRecords.Add(new OciDataTransferRecord
            {
                ClientTenantId = tenant.Id,
                UsageDate = DateOnly.FromDateTime(usageTime),
                OutboundGb = quantity
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<JsonElement[]?> PostUsageQueryAsync(object body, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body, JsonOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/usage")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        _signer.SignRequest(request);

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("OCI API returned {Status}: {Body}", response.StatusCode, errorBody);
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(responseJson);

        if (doc.RootElement.TryGetProperty("items", out var itemsArray))
        {
            return itemsArray.EnumerateArray().ToArray();
        }

        return null;
    }

    private static string GetDimension(JsonElement item, string key)
    {
        if (item.TryGetProperty("tags", out var tags))
        {
            foreach (var tag in tags.EnumerateObject())
            {
                if (tag.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return tag.Value.GetString() ?? string.Empty;
            }
        }

        if (item.TryGetProperty(key, out var direct))
            return direct.GetString() ?? string.Empty;

        return string.Empty;
    }

    private static decimal GetCost(JsonElement item)
    {
        if (item.TryGetProperty("computedAmount", out var amount))
            return amount.GetDecimal();
        if (item.TryGetProperty("cost", out var cost))
            return cost.GetDecimal();
        return 0m;
    }

    private static decimal GetQuantity(JsonElement item)
    {
        if (item.TryGetProperty("computedQuantity", out var qty))
            return qty.GetDecimal();
        if (item.TryGetProperty("quantity", out var q))
            return q.GetDecimal();
        return 0m;
    }
}
