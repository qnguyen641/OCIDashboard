using System.ComponentModel.DataAnnotations;

namespace OracleWebApplication.Models;

public class ClientTenant
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string OciTenancyOcid { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? CompartmentOcid { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedUtc { get; set; }

    public ICollection<ApplicationUser> Users { get; set; } = [];
}
