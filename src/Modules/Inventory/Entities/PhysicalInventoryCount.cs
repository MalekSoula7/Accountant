using ComptaShield.SharedKernel.MultiTenancy;

namespace ComptaShield.Inventory.Entities;

public enum InventoryCountStatus
{
    Draft = 0,
    Validated = 1
}

/// <summary>
/// Physical stock take record (PV d'Inventaire).
/// Scoped to CabinetId, DossierId, ExerciceId.
/// </summary>
public sealed class PhysicalInventoryCount : IDossierScopedEntity
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid ExerciceId { get; set; }

    public string CountReference { get; set; } = string.Empty;
    public DateTime CountDate { get; set; }
    public InventoryCountStatus Status { get; set; } = InventoryCountStatus.Draft;

    public decimal TotalTheoreticalValuation { get; set; }
    public decimal TotalPhysicalValuation { get; set; }
    public decimal TotalDiscrepancyAmount { get; set; }
    public decimal TotalDepreciationProvision { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ValidatedAt { get; set; }
    public Guid? ValidatedByUserId { get; set; }

    public ICollection<PhysicalInventoryLine> Lines { get; set; } = [];
}

public sealed class PhysicalInventoryLine : IDossierScopedEntity
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid ExerciceId { get; set; }

    public Guid PhysicalInventoryCountId { get; set; }
    public Guid StockItemId { get; set; }

    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public decimal TheoreticalQuantity { get; set; }
    public decimal PhysicalQuantity { get; set; }
    public decimal DiscrepancyQuantity { get; set; } // Physical - Theoretical

    public decimal UnitCost { get; set; }
    public decimal FinancialDiscrepancy { get; set; } // DiscrepancyQuantity * UnitCost

    /// <summary>Allowance / provision for obsolete or damaged goods.</summary>
    public decimal DepreciationProvision { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public PhysicalInventoryCount PhysicalInventoryCount { get; set; } = null!;
}
