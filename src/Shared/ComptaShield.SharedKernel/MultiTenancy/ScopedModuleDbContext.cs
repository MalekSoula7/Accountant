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
    /// a query filter that dynamically reads from _tenantContext so EF Core
    /// re-evaluates the current tenant values on every query (parameterized filter pattern).
    /// Filter: e.CabinetId == _tenantContext.CabinetId
    ///      && e.DossierId == _tenantContext.DossierId
    ///      && e.ExerciceId == _tenantContext.ExerciceId
    /// </summary>
    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        // Expression representing 'this' (the DbContext instance)
        var dbContextExpr = Expression.Constant(this);
        // Expression representing 'this._tenantContext'
        var tenantField = Expression.Field(dbContextExpr, typeof(ScopedModuleDbContext)
            .GetField("_tenantContext", BindingFlags.NonPublic | BindingFlags.Instance)!);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (!typeof(IDossierScopedEntity).IsAssignableFrom(clrType))
                continue;

            var parameter = Expression.Parameter(clrType, "e");

            var filter = BuildDynamicEqualityExpression(parameter, tenantField, nameof(IDossierScopedEntity.CabinetId));
            filter = Expression.AndAlso(filter, BuildDynamicEqualityExpression(parameter, tenantField, nameof(IDossierScopedEntity.DossierId)));
            filter = Expression.AndAlso(filter, BuildDynamicEqualityExpression(parameter, tenantField, nameof(IDossierScopedEntity.ExerciceId)));

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

    /// <summary>
    /// Builds: e.{propertyName} == tenantContext.{propertyName}
    /// where tenantContext is accessed dynamically (not a baked-in constant).
    /// </summary>
    private static BinaryExpression BuildDynamicEqualityExpression(
        ParameterExpression parameter, MemberExpression tenantContextExpr, string propertyName)
    {
        var entityProperty = Expression.Property(parameter, propertyName);
        var tenantProperty = Expression.Property(tenantContextExpr, propertyName);
        return Expression.Equal(entityProperty, tenantProperty);
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
