using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OracleWebApplication.Data;
using OracleWebApplication.Models;
using OracleWebApplication.Services;

namespace OracleWebApplication.Pages.Admin.Tenants;

[Authorize(Roles = "Admin")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public CreateModel(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
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
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var tenant = new ClientTenant
        {
            CompanyName = Input.CompanyName,
            OciTenancyOcid = Input.OciTenancyOcid,
            CompartmentOcid = Input.CompartmentOcid,
            Description = Input.Description
        };

        _db.ClientTenants.Add(tenant);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("TenantCreated", User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            User.Identity?.Name, null, HttpContext.Connection.RemoteIpAddress?.ToString(),
            $"Created tenant '{tenant.CompanyName}' (ID {tenant.Id})");

        return RedirectToPage("Index");
    }
}
