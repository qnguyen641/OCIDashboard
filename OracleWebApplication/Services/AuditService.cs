using OracleWebApplication.Data;
using OracleWebApplication.Models;

namespace OracleWebApplication.Services;

public class AuditService
{
    private readonly ApplicationDbContext _db;

    public AuditService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(string eventType, string? userId, string? userName,
        int? clientTenantId, string? ipAddress, string? details)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            EventType = eventType,
            UserId = userId,
            UserName = userName,
            ClientTenantId = clientTenantId,
            IpAddress = ipAddress,
            Details = details
        });
        await _db.SaveChangesAsync();
    }
}
