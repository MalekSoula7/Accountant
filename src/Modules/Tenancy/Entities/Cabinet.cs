namespace ComptaShield.Tenancy.Entities;

/// <summary>
/// Root tenant entity representing an accounting firm (Cabinet Comptable).
/// All Dossiers, users, and data belong to exactly one Cabinet.
/// PK: (Id)
/// </summary>
public sealed class Cabinet
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<Dossier> Dossiers { get; set; } = [];
}
