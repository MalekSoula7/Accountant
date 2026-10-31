namespace ComptaShield.SharedKernel.MultiTenancy;

/// <summary>
/// Marker interface for all entities that are scoped to a specific Dossier within a Cabinet.
/// EF Core Global Query Filters and SaveChanges interceptors use these properties
/// to enforce automatic tenant isolation at the ORM layer.
/// </summary>
public interface IDossierScopedEntity
{
    Guid CabinetId { get; set; }
    Guid DossierId { get; set; }
    Guid ExerciceId { get; set; }
}
