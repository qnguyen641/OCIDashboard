namespace OracleWebApplication.Models;

/// <summary>
/// Cached daily VCN outbound data-transfer record from OCI.
/// One row per tenant × date.
/// </summary>
public class OciDataTransferRecord
{
    public long Id { get; set; }

    public int ClientTenantId { get; set; }
    public ClientTenant ClientTenant { get; set; } = null!;

    public DateOnly UsageDate { get; set; }

    /// <summary>Outbound data transfer in gigabytes.</summary>
    public decimal OutboundGb { get; set; }

    public DateTime FetchedUtc { get; set; } = DateTime.UtcNow;
}
