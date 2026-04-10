using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OracleWebApplication.Data;
using OracleWebApplication.Models;
using OracleWebApplication.Services;

namespace OracleWebApplication.Pages.Admin.Users;

[Authorize(Roles = "Admin")]
public class ToggleActiveModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public ToggleActiveModel(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var user = await _db.Set<ApplicationUser>().FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();

        var action = user.IsActive ? "Activated" : "Deactivated";
        await _audit.LogAsync($"User{action}",
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            User.Identity?.Name, user.ClientTenantId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            $"{action} user '{user.Email}' (ID {user.Id})");

        return RedirectToPage("Index");
    }
}
