using ComptaShield.Tenancy.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.Tenancy.Persistence;

/// <summary>
/// DbContext for the tenancy schema. This context is NOT tenant-scoped (no global query filters)
/// because it manages the tenant hierarchy itself (Cabinets, Dossiers, Exercices, Assignments).
/// </summary>
public sealed class TenancyDbContext : DbContext
{
    public TenancyDbContext(DbContextOptions<TenancyDbContext> options) : base(options) { }

    public DbSet<Cabinet> Cabinets => Set<Cabinet>();
    public DbSet<Dossier> Dossiers => Set<Dossier>();
    public DbSet<Exercice> Exercices => Set<Exercice>();
    public DbSet<DossierAssignment> DossierAssignments => Set<DossierAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("tenancy");

        // Cabinet
        modelBuilder.Entity<Cabinet>(e =>
        {
            e.ToTable("cabinets");
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).HasMaxLength(200).IsRequired();
            e.Property(c => c.TaxId).HasMaxLength(50);
            e.Property(c => c.Address).HasMaxLength(500);
        });

        // Dossier — composite PK (CabinetId, Id) with unique on Id
        modelBuilder.Entity<Dossier>(e =>
        {
            e.ToTable("dossiers");
            e.HasKey(d => new { d.CabinetId, d.Id });
            e.HasIndex(d => d.Id).IsUnique();
            e.Property(d => d.CompanyName).HasMaxLength(200).IsRequired();
            e.Property(d => d.MatriculeFiscal).HasMaxLength(50);
            e.Property(d => d.Address).HasMaxLength(500);
            e.HasOne(d => d.Cabinet)
                .WithMany(c => c.Dossiers)
                .HasForeignKey(d => d.CabinetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Exercice — composite PK (CabinetId, DossierId, Id) with unique on Id
        modelBuilder.Entity<Exercice>(e =>
        {
            e.ToTable("exercices");
            e.HasKey(ex => new { ex.CabinetId, ex.DossierId, ex.Id });
            e.HasIndex(ex => ex.Id).IsUnique();
            e.Property(ex => ex.Label).HasMaxLength(100).IsRequired();
            e.HasOne(ex => ex.Dossier)
                .WithMany(d => d.Exercices)
                .HasForeignKey(ex => new { ex.CabinetId, ex.DossierId })
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DossierAssignment — composite PK (CabinetId, DossierId, UserId)
        modelBuilder.Entity<DossierAssignment>(e =>
        {
            e.ToTable("dossier_assignments");
            e.HasKey(a => new { a.CabinetId, a.DossierId, a.UserId });
            e.Property(a => a.Role).HasConversion<string>().HasMaxLength(30);
            e.HasOne(a => a.Dossier)
                .WithMany(d => d.Assignments)
                .HasForeignKey(a => new { a.CabinetId, a.DossierId })
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
