using ComptaShield.SharedKernel.Enums;

namespace ComptaShield.Tenancy.Services;

/// <summary>
/// Checks whether a user has access to a specific Dossier within a Cabinet.
/// Implementations should use Redis for fast lookup with DB fallback.
/// </summary>
public interface IPermissionChecker
{
    /// <summary>
    /// Returns the user's role for the given dossier, or null if no assignment exists.
    /// </summary>
    Task<DossierRole?> GetUserRoleAsync(Guid cabinetId, Guid userId, Guid dossierId, CancellationToken ct = default);

    /// <summary>
    /// Invalidates the cached permissions for a user within a cabinet.
    /// Called when DossierAssignment changes.
    /// </summary>
    Task InvalidateCacheAsync(Guid cabinetId, Guid userId, CancellationToken ct = default);
}
