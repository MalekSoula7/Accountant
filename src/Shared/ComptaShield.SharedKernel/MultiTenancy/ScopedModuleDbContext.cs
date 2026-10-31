using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ComptaShield.SharedKernel.MultiTenancy;

/// <summary>
/// Base DbContext for all domain modules. Applies automatic tenant isolation via:
/// 1. EF Core Global Query Filters on all IDossierScopedEntity entities.
/// 2. SaveChanges override that stamps CabinetId/DossierId/ExerciceId on Added entities,
///    blocks mutation of tenant keys on Modified/Deleted, and rejects writes when Exercice is locked.
/// </summary>
public abstract class ScopedModuleDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    protected ScopedModuleDbContext(
        DbContextOptions options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ApplyGlobalQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Scans all entity types implementing IDossierScopedEntity and applies
    /// a query filter: e.CabinetId == _tenantContext.CabinetId
    ///                  && e.DossierId == _tenantContext.DossierId
    ///                  && e.ExerciceId == _tenantContext.ExerciceId
    /// </summary>
    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (!typeof(IDossierScopedEntity).IsAssignableFrom(clrType))
                continue;

            var parameter = Expression.Parameter(clrType, "e");

            var filter = BuildEqualityExpression(parameter, nameof(IDossierScopedEntity.CabinetId), _tenantContext.CabinetId);
            filter = Expression.AndAlso(filter, BuildEqualityExpression(parameter, nameof(IDossierScopedEntity.DossierId), _tenantContext.DossierId));
            filter = Expression.AndAlso(filter, BuildEqualityExpression(parameter, nameof(IDossierScopedEntity.ExerciceId), _tenantContext.ExerciceId));

            var lambda = Expression.Lambda(filter, parameter);

            // modelBuilder.Entity(clrType).HasQueryFilter(lambda)
            var entityMethod = typeof(ModelBuilder)
                .GetMethods()
                .First(m => m.Name == nameof(ModelBuilder.Entity) && m.IsGenericMethod && m.GetParameters().Length == 0)
                .MakeGenericMethod(clrType);

            var entityBuilder = entityMethod.Invoke(modelBuilder, null)!;

            var hasQueryFilterMethod = entityBuilder.GetType()
                .GetMethods()
                .First(m => m.Name == nameof(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.HasQueryFilter));

            hasQueryFilterMethod.Invoke(entityBuilder, [lambda]);
        }
    }

    private static BinaryExpression BuildEqualityExpression(ParameterExpression parameter, string propertyName, Guid value)
    {
        var property = Expression.Property(parameter, propertyName);
        var constant = Expression.Constant(value);
        return Expression.Equal(property, constant);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceTenantInvariants();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceTenantInvariants();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnforceTenantInvariants()
    {
        if (_tenantContext.IsExerciceLocked)
        {
            var hasScopedChanges = ChangeTracker.Entries()
                .Any(e => e.Entity is IDossierScopedEntity &&
                          e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

            if (hasScopedChanges)
                throw new InvalidOperationException(
                    $"Exercice {_tenantContext.ExerciceId} is locked. No writes are permitted on locked fiscal years.");
        }

        foreach (var entry in ChangeTracker.Entries<IDossierScopedEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CabinetId = _tenantContext.CabinetId;
                    entry.Entity.DossierId = _tenantContext.DossierId;
                    entry.Entity.ExerciceId = _tenantContext.ExerciceId;
                    break;

                case EntityState.Modified:
                case EntityState.Deleted:
                    EnsureTenantKeysUnchanged(entry);
                    break;
            }
        }
    }

    private static void EnsureTenantKeysUnchanged(EntityEntry<IDossierScopedEntity> entry)
    {
        var cabinetProp = entry.Property(e => e.CabinetId);
        var dossierProp = entry.Property(e => e.DossierId);
        var exerciceProp = entry.Property(e => e.ExerciceId);

        if (cabinetProp.IsModified || dossierProp.IsModified || exerciceProp.IsModified)
            throw new InvalidOperationException(
                "Mutation of tenant keys (CabinetId, DossierId, ExerciceId) is forbidden on existing entities.");
    }
}
