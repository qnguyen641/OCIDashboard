using System.ComponentModel.DataAnnotations;

namespace OracleWebApplication.Models;

public class AuditLog
{
    public long Id { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty;

    public string? UserId { get; set; }

    [MaxLength(256)]
    public string? UserName { get; set; }

    public int? ClientTenantId { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [MaxLength(1000)]
    public string? Details { get; set; }
}
