namespace ComptaShield.Tenancy.Entities;

/// <summary>
/// Represents a client company (Dossier) within a Cabinet.
/// Composite PK: (CabinetId, Id) with UNIQUE(Id).
/// </summary>
public sealed class Dossier
{
    public Guid CabinetId { get; set; }
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string MatriculeFiscal { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public Cabinet Cabinet { get; set; } = null!;
    public ICollection<Exercice> Exercices { get; set; } = [];
    public ICollection<DossierAssignment> Assignments { get; set; } = [];
}
