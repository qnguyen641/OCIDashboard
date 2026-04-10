using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OracleWebApplication.Data;
using OracleWebApplication.Models;

namespace OracleWebApplication.Pages.Admin.Tenants;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<ClientTenant> Tenants { get; set; } = [];

    public async Task OnGetAsync()
    {
        Tenants = await _db.ClientTenants
            .OrderBy(t => t.CompanyName)
            .ToListAsync();
    }
}
