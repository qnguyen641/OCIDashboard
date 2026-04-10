using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace OracleWebApplication.Models;

public class ApplicationUser : IdentityUser
{
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public int ClientTenantId { get; set; }

    public ClientTenant ClientTenant { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginUtc { get; set; }
}
