using ComptaShield.SharedKernel.Enums;

namespace ComptaShield.SharedKernel.MultiTenancy;

/// <summary>
/// Read-only tenant context resolved per-request from JWT + HTTP headers.
/// Provides the scoping keys used by EF Core Global Query Filters and PostgreSQL RLS.
/// </summary>
public interface ITenantContext
{
    Guid UserId { get; }
    Guid CabinetId { get; }
    Guid DossierId { get; }
    Guid ExerciceId { get; }
    DossierRole Role { get; }
    bool IsExerciceLocked { get; }
}
