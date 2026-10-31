using ComptaShield.Ingestion.Entities;
using ComptaShield.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.Ingestion.Persistence;

/// <summary>
/// DbContext for the ingestion schema. Extends ScopedModuleDbContext
/// for automatic tenant isolation via Global Query Filters and RLS.
/// </summary>
public sealed class IngestionDbContext : ScopedModuleDbContext
{
    public IngestionDbContext(DbContextOptions<IngestionDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext) { }

    public DbSet<IngestedDocument> Documents => Set<IngestedDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("ingestion");

        modelBuilder.Entity<IngestedDocument>(e =>
        {
            e.ToTable("documents");
            e.HasKey(d => d.Id);
            e.Property(d => d.OriginalFileName).HasMaxLength(500).IsRequired();
            e.Property(d => d.FileExtension).HasMaxLength(10).IsRequired();
            e.Property(d => d.Sha256Hash).HasMaxLength(64).IsRequired();
            e.Property(d => d.ObjectStorageKey).HasMaxLength(500).IsRequired();
            e.Property(d => d.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(d => d.Source).HasConversion<string>().HasMaxLength(20);
            e.Property(d => d.ExtractedDataJson).HasColumnType("jsonb");
            e.Property(d => d.OcrConfidence).HasColumnType("numeric(5,4)");

            // SHA-256 dedup index scoped per (CabinetId, DossierId)
            e.HasIndex(d => new { d.CabinetId, d.DossierId, d.Sha256Hash }).IsUnique();
        });
    }
}
