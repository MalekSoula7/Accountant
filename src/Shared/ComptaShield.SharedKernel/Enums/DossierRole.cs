namespace ComptaShield.SharedKernel.Enums;

/// <summary>
/// Role assigned to a user within a specific Dossier (client company).
/// Determines granular permissions via DossierAssignment flags.
/// </summary>
public enum DossierRole
{
    /// <summary>Full access to all Dossiers within the Cabinet.</summary>
    Partner = 0,

    /// <summary>Senior auditor with validation rights on assigned Dossiers.</summary>
    SeniorAuditor = 1,

    /// <summary>Junior collaborator with limited write access on assigned Dossiers.</summary>
    JuniorCollaborator = 2,

    /// <summary>External client guest with read-only access + document upload to their own Dossier.</summary>
    ClientGuest = 3
}
