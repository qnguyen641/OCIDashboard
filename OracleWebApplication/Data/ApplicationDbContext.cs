using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OracleWebApplication.Models;

namespace OracleWebApplication.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ClientTenant> ClientTenants => Set<ClientTenant>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OciCostRecord> OciCostRecords => Set<OciCostRecord>();
    public DbSet<OciDataTransferRecord> OciDataTransferRecords => Set<OciDataTransferRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ClientTenant>(entity =>
        {
            entity.HasIndex(e => e.CompanyName).IsUnique();
            entity.HasIndex(e => e.OciTenancyOcid);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasOne(u => u.ClientTenant)
                  .WithMany(t => t.Users)
                  .HasForeignKey(u => u.ClientTenantId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(e => e.TimestampUtc);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ClientTenantId);
        });

        builder.Entity<OciCostRecord>(entity =>
        {
            entity.HasIndex(e => new { e.ClientTenantId, e.UsageDate });
            entity.HasIndex(e => e.Service);
            entity.HasIndex(e => e.Region);
            entity.Property(e => e.Cost).HasPrecision(18, 6);

            entity.HasOne(e => e.ClientTenant)
                  .WithMany()
                  .HasForeignKey(e => e.ClientTenantId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OciDataTransferRecord>(entity =>
        {
            entity.HasIndex(e => new { e.ClientTenantId, e.UsageDate }).IsUnique();
            entity.Property(e => e.OutboundGb).HasPrecision(18, 6);

            entity.HasOne(e => e.ClientTenant)
                  .WithMany()
                  .HasForeignKey(e => e.ClientTenantId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
