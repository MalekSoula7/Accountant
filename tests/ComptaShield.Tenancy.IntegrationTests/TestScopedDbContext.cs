using ComptaShield.SharedKernel.Enums;
using ComptaShield.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.Tenancy.IntegrationTests;

/// <summary>
/// A concrete ScopedModuleDbContext for testing with a sample tenant-scoped entity.
/// </summary>
public sealed class TestScopedDbContext : ScopedModuleDbContext
{
    public TestScopedDbContext(DbContextOptions<TestScopedDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext) { }

    public DbSet<TestDocument> Documents => Set<TestDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<TestDocument>(e =>
        {
            e.HasKey(d => d.Id);
        });
    }
}

/// <summary>
/// Sample entity implementing IDossierScopedEntity for testing global query filters.
/// </summary>
public sealed class TestDocument : IDossierScopedEntity
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid ExerciceId { get; set; }
    public string Title { get; set; } = string.Empty;
}
