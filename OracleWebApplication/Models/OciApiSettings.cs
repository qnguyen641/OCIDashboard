namespace OracleWebApplication.Models;

/// <summary>
/// Configuration for OCI REST API authentication.
/// Bound from the "OciApi" section in appsettings.json.
/// In production, store sensitive values (PrivateKeyPath) in a secrets manager.
/// </summary>
public class OciApiSettings
{
    public const string SectionName = "OciApi";

    /// <summary>OCID of the tenancy.</summary>
    public string TenancyOcid { get; set; } = string.Empty;

    /// <summary>OCID of the API user.</summary>
    public string UserOcid { get; set; } = string.Empty;

    /// <summary>Fingerprint of the uploaded API public key.</summary>
    public string KeyFingerprint { get; set; } = string.Empty;

    /// <summary>File-system path to the PEM private key.</summary>
    public string PrivateKeyPath { get; set; } = string.Empty;

    /// <summary>OCI region identifier, e.g. "us-ashburn-1".</summary>
    public string Region { get; set; } = "us-ashburn-1";

    /// <summary>Hours between automatic data refreshes. Default 24.</summary>
    public int RefreshIntervalHours { get; set; } = 24;
}
