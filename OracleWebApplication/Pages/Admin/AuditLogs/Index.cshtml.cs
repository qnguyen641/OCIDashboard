using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OracleWebApplication.Data;
using OracleWebApplication.Models;

namespace OracleWebApplication.Pages.Admin.AuditLogs;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<AuditLog> Logs { get; set; } = [];

    public async Task OnGetAsync()
    {
        Logs = await _db.AuditLogs
            .OrderByDescending(l => l.TimestampUtc)
            .Take(200)
            .ToListAsync();
    }
}
