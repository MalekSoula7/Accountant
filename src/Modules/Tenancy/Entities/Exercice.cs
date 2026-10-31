namespace ComptaShield.Tenancy.Entities;

/// <summary>
/// Represents a fiscal year (Exercice) within a Dossier.
/// Composite PK: (CabinetId, DossierId, Id) with UNIQUE(Id).
/// FK: (CabinetId, DossierId) -> Dossier(CabinetId, Id).
/// </summary>
public sealed class Exercice
{
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Dossier Dossier { get; set; } = null!;
}
