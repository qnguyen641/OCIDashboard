using System.ComponentModel.DataAnnotations;

namespace OracleWebApplication.Models;

/// <summary>
/// Cached daily cost record pulled from the OCI Usage API.
/// One row per tenant × date × service × region.
/// </summary>
public class OciCostRecord
{
    public long Id { get; set; }

    public int ClientTenantId { get; set; }
    public ClientTenant ClientTenant { get; set; } = null!;

    public DateOnly UsageDate { get; set; }

    [Required]
    [MaxLength(100)]
    public string Service { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Region { get; set; } = string.Empty;

    public decimal Cost { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    public DateTime FetchedUtc { get; set; } = DateTime.UtcNow;
}
