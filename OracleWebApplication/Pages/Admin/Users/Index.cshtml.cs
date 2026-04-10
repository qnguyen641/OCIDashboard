using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OracleWebApplication.Data;
using OracleWebApplication.Models;

namespace OracleWebApplication.Pages.Admin.Users;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public List<UserRow> Users { get; set; } = [];

    public class UserRow
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime? LastLoginUtc { get; set; }
    }

    public async Task OnGetAsync()
    {
        var users = await _db.Set<ApplicationUser>()
            .Include(u => u.ClientTenant)
            .OrderBy(u => u.Email)
            .ToListAsync();

        foreach (var u in users)
        {
            Users.Add(new UserRow
            {
                Id = u.Id,
                Email = u.Email ?? string.Empty,
                DisplayName = u.DisplayName,
                TenantName = u.ClientTenant?.CompanyName ?? "—",
                IsActive = u.IsActive,
                IsAdmin = await _userManager.IsInRoleAsync(u, "Admin"),
                LastLoginUtc = u.LastLoginUtc
            });
        }
    }
}
