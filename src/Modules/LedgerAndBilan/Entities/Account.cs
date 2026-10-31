using ComptaShield.SharedKernel.MultiTenancy;

namespace ComptaShield.LedgerAndBilan.Entities;

/// <summary>
/// Account in the hierarchical Chart of Accounts (Plan Comptable).
/// Supports SCE and PCG classification (Classes 1 through 7).
/// </summary>
public sealed class Account : IDossierScopedEntity
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid ExerciceId { get; set; }

    public string AccountNumber { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int AccountClass { get; set; } // 1 to 7
    public bool IsReconcilable { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
