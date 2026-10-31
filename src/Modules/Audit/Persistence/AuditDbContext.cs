using ComptaShield.Audit.Entities;
using ComptaShield.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.Audit.Persistence;

public sealed class AuditDbContext : ScopedModuleDbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext) { }

    public DbSet<AuditEngagement> Engagements => Set<AuditEngagement>();
    public DbSet<AuditSampleItem> SampleItems => Set<AuditSampleItem>();
    public DbSet<AuditDeliverable> Deliverables => Set<AuditDeliverable>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("audit");

        modelBuilder.Entity<AuditEngagement>(e =>
        {
            e.ToTable("engagements");
            e.HasKey(a => a.Id);
            e.Property(a => a.Title).HasMaxLength(250).IsRequired();
            e.Property(a => a.Benchmark).HasConversion<string>().HasMaxLength(30);
            e.Property(a => a.BenchmarkAmount).HasColumnType("numeric(19,4)");
            e.Property(a => a.MaterialityPercentage).HasColumnType("numeric(5,4)");
            e.Property(a => a.PlanningMateriality).HasColumnType("numeric(19,4)");
            e.Property(a => a.PerformanceMateriality).HasColumnType("numeric(19,4)");
            e.Property(a => a.ClearlyTrivialThreshold).HasColumnType("numeric(19,4)");
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(30);

            e.HasIndex(a => new { a.CabinetId, a.DossierId, a.ExerciceId });
        });

        modelBuilder.Entity<AuditSampleItem>(e =>
        {
            e.ToTable("sample_items");
            e.HasKey(s => s.Id);
            e.Property(s => s.PopulationName).HasMaxLength(100).IsRequired();
            e.Property(s => s.ItemIdentifier).HasMaxLength(100).IsRequired();
            e.Property(s => s.Description).HasMaxLength(500);
            e.Property(s => s.BookValue).HasColumnType("numeric(19,4)");
            e.Property(s => s.Method).HasConversion<string>().HasMaxLength(30);
            e.Property(s => s.Stratum).HasMaxLength(50);
            e.Property(s => s.AuditorNotes).HasMaxLength(1000);

            e.HasOne(s => s.AuditEngagement)
                .WithMany(a => a.Samples)
                .HasForeignKey(s => s.AuditEngagementId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(s => new { s.CabinetId, s.DossierId, s.AuditEngagementId });
        });

        modelBuilder.Entity<AuditDeliverable>(e =>
        {
            e.ToTable("deliverables");
            e.HasKey(d => d.Id);
            e.Property(d => d.DeliverableType).HasMaxLength(50).IsRequired();
            e.Property(d => d.Version).HasMaxLength(20).IsRequired();
            e.Property(d => d.Sha256Hash).HasMaxLength(64).IsRequired();
            e.Property(d => d.StorageKey).HasMaxLength(500).IsRequired();
            e.Property(d => d.CryptographicSignature).HasMaxLength(256);

            e.HasIndex(d => new { d.CabinetId, d.DossierId, d.ExerciceId, d.Sha256Hash });
        });
    }
}
