using ComptaShield.SharedKernel.Enums;

namespace ComptaShield.Tenancy.Entities;

/// <summary>
/// RBAC assignment linking a user to a specific Dossier within a Cabinet.
/// Composite PK: (CabinetId, DossierId, UserId).
/// Contains role and granular permission flags.
/// </summary>
public sealed class DossierAssignment
{
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid UserId { get; set; }
    public DossierRole Role { get; set; }

    // Granular permission flags
    public bool CanValidateOcr { get; set; }
    public bool CanPostEntries { get; set; }
    public bool CanLockBilan { get; set; }

    public DateTime AssignedAt { get; set; }

    // Navigation
    public Dossier Dossier { get; set; } = null!;
}
