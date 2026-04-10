using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OracleWebApplication.Data;
using OracleWebApplication.Models;
using OracleWebApplication.Services;

namespace OracleWebApplication.Pages.Admin.Tenants;

[Authorize(Roles = "Admin")]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public DeleteModel(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public ClientTenant Tenant { get; set; } = null!;
    public int UserCount { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var tenant = await _db.ClientTenants.FindAsync(id);
        if (tenant is null)
        {
            return NotFound();
        }

        Tenant = tenant;
        UserCount = await _db.Users.CountAsync(u => ((ApplicationUser)u).ClientTenantId == id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var tenant = await _db.ClientTenants.FindAsync(id);
        if (tenant is null)
        {
            return NotFound();
        }

        var hasUsers = await _db.Users.AnyAsync(u => ((ApplicationUser)u).ClientTenantId == id);
        if (hasUsers)
        {
            ModelState.AddModelError(string.Empty, "Cannot delete a tenant that still has users assigned.");
            Tenant = tenant;
            UserCount = await _db.Users.CountAsync(u => ((ApplicationUser)u).ClientTenantId == id);
            return Page();
        }

        var name = tenant.CompanyName;
        _db.ClientTenants.Remove(tenant);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("TenantDeleted", User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            User.Identity?.Name, null, HttpContext.Connection.RemoteIpAddress?.ToString(),
            $"Deleted tenant '{name}' (ID {id})");

        return RedirectToPage("Index");
    }
}
