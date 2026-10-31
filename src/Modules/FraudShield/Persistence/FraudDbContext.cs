using ComptaShield.FraudShield.Entities;
using ComptaShield.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.FraudShield.Persistence;

public sealed class FraudDbContext : ScopedModuleDbContext
{
    public FraudDbContext(DbContextOptions<FraudDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext) { }

    public DbSet<FraudAlert> Alerts => Set<FraudAlert>();
    public DbSet<IndexedInvoiceFingerprint> Fingerprints => Set<IndexedInvoiceFingerprint>();
    public DbSet<ClientClarificationTicket> ClarificationTickets => Set<ClientClarificationTicket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("fraud");

        modelBuilder.Entity<ClientClarificationTicket>(e =>
        {
            e.ToTable("clarification_tickets");
            e.HasKey(t => t.Id);
            e.Property(t => t.MagicToken).HasMaxLength(128).IsRequired();
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(t => t.QuestionPrompt).HasMaxLength(1000);
            e.Property(t => t.ClientExplanation).HasMaxLength(4000);
            e.Property(t => t.SupportingDocumentKey).HasMaxLength(500);

            e.HasIndex(t => t.MagicToken).IsUnique();
            e.HasIndex(t => new { t.CabinetId, t.DossierId, t.FraudAlertId });
        });

        modelBuilder.Entity<FraudAlert>(e =>
        {
            e.ToTable("fraud_alerts");
            e.HasKey(a => a.Id);
            e.Property(a => a.Title).HasMaxLength(250).IsRequired();
            e.Property(a => a.AlertType).HasConversion<string>().HasMaxLength(50);
            e.Property(a => a.Severity).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(a => a.RiskScore).HasColumnType("numeric(5,4)");
            e.Property(a => a.ForensicEvidenceJson).HasColumnType("jsonb");

            e.HasIndex(a => new { a.CabinetId, a.DossierId, a.ExerciceId });
        });

        modelBuilder.Entity<IndexedInvoiceFingerprint>(e =>
        {
            e.ToTable("invoice_fingerprints");
            e.HasKey(f => f.Id);
            e.Property(f => f.SupplierMatriculeFiscal).HasMaxLength(50).IsRequired();
            e.Property(f => f.NormalizedInvoiceNumber).HasMaxLength(100).IsRequired();
            e.Property(f => f.OriginalInvoiceNumber).HasMaxLength(100);
            e.Property(f => f.TotalTTC).HasColumnType("numeric(19,4)");

            // Composite indexes for fast matching across current and prior exercices
            e.HasIndex(f => new { f.CabinetId, f.DossierId, f.SupplierMatriculeFiscal, f.TotalTTC });
            e.HasIndex(f => new { f.CabinetId, f.DossierId, f.NormalizedInvoiceNumber });
        });
    }
}
