using ComptaShield.SharedKernel.Enums;

namespace ComptaShield.SharedKernel.MultiTenancy;

/// <summary>
/// Mutable implementation of <see cref="ITenantContext"/> populated by TenantScopeMiddleware.
/// Registered as Scoped in DI so each HTTP request / message consumer gets its own instance.
/// </summary>
public sealed class ScopedTenantContext : ITenantContext
{
    public Guid UserId { get; set; }
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid ExerciceId { get; set; }
    public DossierRole Role { get; set; }
    public bool IsExerciceLocked { get; set; }
}
