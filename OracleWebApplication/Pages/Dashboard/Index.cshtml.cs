using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OracleWebApplication.Data;
using OracleWebApplication.Models;
using System.Text.Json;

namespace OracleWebApplication.Pages.Dashboard;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public string TenantName { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;

    // Summary
    public decimal TotalCost { get; set; }
    public decimal CurrentMonthCost { get; set; }

    // Date range (full data range)
    public DateOnly DataRangeStart { get; set; }
    public DateOnly DataRangeEnd { get; set; }

    // Active filter range
    public DateOnly RangeStart { get; set; }
    public DateOnly RangeEnd { get; set; }

    // Last updated
    public DateTime? LastUpdatedUtc { get; set; }

    // Filter selections
    [BindProperty(SupportsGet = true)]
    public string? FilterStartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterEndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string[]? FilterRegions { get; set; }

    [BindProperty(SupportsGet = true)]
    public string[]? FilterServices { get; set; }

    // Available options for filter dropdowns
    public string[] AllRegions { get; set; } = [];
    public string[] AllServices { get; set; } = [];

    // Chart data serialized as JSON for Chart.js
    public string MonthlyCostJson { get; set; } = "{}";
    public string DailyCostJson { get; set; } = "{}";
    public string ServiceStackedJson { get; set; } = "{}";
    public string RegionPieJson { get; set; } = "{}";
    public string TransferChartJson { get; set; } = "{}";

    // Table data
    public List<DailyServiceRow> DailyServiceRows { get; set; } = [];
    public List<RegionalServiceRow> RegionalServiceRows { get; set; } = [];
    public string[] ServiceColumns { get; set; } = [];
    public string[] RegionColumns { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return RedirectToPage("/Account/Login");

        var tenant = await _db.ClientTenants.FindAsync(user.ClientTenantId);
        if (tenant is null) return RedirectToPage("/Index");

        TenantName = tenant.CompanyName;
        UserDisplayName = user.DisplayName;
        UserEmail = user.Email ?? string.Empty;

        // Load all cost records for this tenant
        var allCostRecords = await _db.OciCostRecords
            .Where(r => r.ClientTenantId == tenant.Id)
            .ToListAsync();

        if (allCostRecords.Count == 0) return Page();

        // Determine full data range and populate filter options
        var allDates = allCostRecords.Select(r => r.UsageDate).Distinct().OrderBy(d => d).ToList();
        DataRangeStart = allDates.First();
        DataRangeEnd = allDates.Last();

        AllRegions = allCostRecords.Select(r => r.Region).Distinct().OrderBy(r => r).ToArray();
        AllServices = allCostRecords.Select(r => r.Service).Distinct().OrderBy(s => s).ToArray();

        // Last updated timestamp
        LastUpdatedUtc = allCostRecords.Max(r => r.FetchedUtc);

        // Apply date filters (default to full range)
        RangeStart = DateOnly.TryParse(FilterStartDate, out var fs) ? fs : DataRangeStart;
        RangeEnd = DateOnly.TryParse(FilterEndDate, out var fe) ? fe : DataRangeEnd;

        // Clamp to data range
        if (RangeStart < DataRangeStart) RangeStart = DataRangeStart;
        if (RangeEnd > DataRangeEnd) RangeEnd = DataRangeEnd;

        // Apply filters
        var costRecords = allCostRecords
            .Where(r => r.UsageDate >= RangeStart && r.UsageDate <= RangeEnd)
            .ToList();

        if (FilterRegions is { Length: > 0 })
            costRecords = costRecords.Where(r => FilterRegions.Contains(r.Region)).ToList();

        if (FilterServices is { Length: > 0 })
            costRecords = costRecords.Where(r => FilterServices.Contains(r.Service)).ToList();

        // Transfer records (filter by date range only)
        var transferRecords = await _db.OciDataTransferRecords
            .Where(r => r.ClientTenantId == tenant.Id
                && r.UsageDate >= RangeStart && r.UsageDate <= RangeEnd)
            .OrderBy(r => r.UsageDate)
            .ToListAsync();

        if (costRecords.Count == 0) return Page();

        TotalCost = costRecords.Sum(r => r.Cost);

        var currentMonth = new DateOnly(RangeEnd.Year, RangeEnd.Month, 1);
        CurrentMonthCost = costRecords.Where(r => r.UsageDate >= currentMonth).Sum(r => r.Cost);

        // ── Monthly cost bar chart ──
        var monthly = costRecords
            .GroupBy(r => new { r.UsageDate.Year, r.UsageDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new
            {
                Label = $"{g.Key.Year}-{g.Key.Month:D2}",
                Total = Math.Round(g.Sum(r => r.Cost), 2)
            }).ToList();

        MonthlyCostJson = JsonSerializer.Serialize(new
        {
            labels = monthly.Select(m => m.Label),
            values = monthly.Select(m => m.Total)
        });

        // ── Daily cost bar chart (current month within filter range) ──
        var dailyCosts = costRecords
            .Where(r => r.UsageDate >= currentMonth)
            .GroupBy(r => r.UsageDate)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Label = g.Key.ToString("yyyy-MM-dd"),
                Total = Math.Round(g.Sum(r => r.Cost), 2)
            }).ToList();

        DailyCostJson = JsonSerializer.Serialize(new
        {
            labels = dailyCosts.Select(d => d.Label),
            values = dailyCosts.Select(d => d.Total)
        });

        // ── Service stacked bar chart (current month, by day) ──
        ServiceColumns = costRecords.Select(r => r.Service).Distinct().OrderBy(s => s).ToArray();
        var dailyDates = costRecords
            .Where(r => r.UsageDate >= currentMonth)
            .Select(r => r.UsageDate).Distinct().OrderBy(d => d).ToList();

        var serviceDatasets = new List<object>();
        foreach (var svc in ServiceColumns)
        {
            var svcCosts = dailyDates.Select(d =>
                Math.Round(costRecords
                    .Where(r => r.UsageDate == d && r.Service == svc)
                    .Sum(r => r.Cost), 2)
            ).ToList();
            serviceDatasets.Add(new { label = svc, data = svcCosts });
        }

        ServiceStackedJson = JsonSerializer.Serialize(new
        {
            labels = dailyDates.Select(d => d.ToString("MM-dd")),
            datasets = serviceDatasets
        });

        // ── Region pie chart ──
        RegionColumns = costRecords.Select(r => r.Region).Distinct().OrderBy(r => r).ToArray();
        var regionTotals = costRecords
            .GroupBy(r => r.Region)
            .Select(g => new { Region = g.Key, Total = Math.Round(g.Sum(r => r.Cost), 2) })
            .OrderByDescending(r => r.Total)
            .ToList();

        RegionPieJson = JsonSerializer.Serialize(new
        {
            labels = regionTotals.Select(r => r.Region),
            values = regionTotals.Select(r => r.Total)
        });

        // ── Outbound data transfer area chart ──
        TransferChartJson = JsonSerializer.Serialize(new
        {
            labels = transferRecords.Select(r => r.UsageDate.ToString("yyyy-MM-dd")),
            values = transferRecords.Select(r => Math.Round(r.OutboundGb, 2))
        });

        // ── Daily service cost table (current month) ──
        DailyServiceRows = dailyDates.Select(d => new DailyServiceRow
        {
            Date = d.ToString("yyyy-MM-dd"),
            ServiceCosts = ServiceColumns.ToDictionary(
                svc => svc,
                svc => Math.Round(costRecords
                    .Where(r => r.UsageDate == d && r.Service == svc)
                    .Sum(r => r.Cost), 2)
            )
        }).ToList();

        // ── Regional service cost table ──
        RegionalServiceRows = RegionColumns.Select(reg => new RegionalServiceRow
        {
            Region = reg,
            ServiceCosts = ServiceColumns.ToDictionary(
                svc => svc,
                svc => Math.Round(costRecords
                    .Where(r => r.Region == reg && r.Service == svc)
                    .Sum(r => r.Cost), 2)
            )
        }).ToList();

        return Page();
    }

    public class DailyServiceRow
    {
        public string Date { get; set; } = string.Empty;
        public Dictionary<string, decimal> ServiceCosts { get; set; } = [];
    }

    public class RegionalServiceRow
    {
        public string Region { get; set; } = string.Empty;
        public Dictionary<string, decimal> ServiceCosts { get; set; } = [];
    }
}
