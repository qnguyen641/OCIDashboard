using Microsoft.Extensions.Options;
using OracleWebApplication.Models;

namespace OracleWebApplication.Services;

/// <summary>
/// Background service that periodically refreshes OCI cost/usage data
/// for all active tenants. Runs on the interval defined in OciApiSettings.
/// </summary>
public class OciDataRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OciApiSettings _settings;
    private readonly ILogger<OciDataRefreshService> _logger;

    public OciDataRefreshService(
        IServiceScopeFactory scopeFactory,
        IOptions<OciApiSettings> settings,
        ILogger<OciDataRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // If OCI API is not configured, seed mock data instead
        if (string.IsNullOrWhiteSpace(_settings.TenancyOcid))
        {
            _logger.LogWarning("OCI API is not configured — seeding mock data for development.");
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var mockService = scope.ServiceProvider.GetRequiredService<MockOciDataService>();
                await mockService.SeedMockDataAsync(stoppingToken);
                _logger.LogInformation("Mock OCI data seeded successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed mock OCI data.");
            }
            return;
        }

        var interval = TimeSpan.FromHours(_settings.RefreshIntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Starting OCI data refresh cycle...");
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var apiService = scope.ServiceProvider.GetRequiredService<OciUsageApiService>();
                await apiService.RefreshAllTenantsAsync(stoppingToken);
                _logger.LogInformation("OCI data refresh cycle completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCI data refresh cycle failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
