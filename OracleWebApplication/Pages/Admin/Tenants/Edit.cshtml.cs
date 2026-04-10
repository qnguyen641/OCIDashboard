using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OracleWebApplication.Data;
using OracleWebApplication.Models;
using OracleWebApplication.Services;

namespace OracleWebApplication.Pages.Admin.Tenants;

[Authorize(Roles = "Admin")]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public EditModel(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        [Display(Name = "OCI Tenancy OCID")]
        public string OciTenancyOcid { get; set; } = string.Empty;

        [MaxLength(200)]
        [Display(Name = "Compartment OCID")]
        public string? CompartmentOcid { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var tenant = await _db.ClientTenants.FindAsync(id);
        if (tenant is null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = tenant.Id,
            CompanyName = tenant.CompanyName,
            OciTenancyOcid = tenant.OciTenancyOcid,
            CompartmentOcid = tenant.CompartmentOcid,
            Description = tenant.Description,
            IsActive = tenant.IsActive
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var tenant = await _db.ClientTenants.FindAsync(Input.Id);
        if (tenant is null)
        {
            return NotFound();
        }

        tenant.CompanyName = Input.CompanyName;
        tenant.OciTenancyOcid = Input.OciTenancyOcid;
        tenant.CompartmentOcid = Input.CompartmentOcid;
        tenant.Description = Input.Description;
        tenant.IsActive = Input.IsActive;
        tenant.UpdatedUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _audit.LogAsync("TenantUpdated", User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            User.Identity?.Name, tenant.Id, HttpContext.Connection.RemoteIpAddress?.ToString(),
            $"Updated tenant '{tenant.CompanyName}' (ID {tenant.Id})");

        return RedirectToPage("Index");
    }
}
