using ComptaShield.SharedKernel.MultiTenancy;

namespace ComptaShield.Inventory.Entities;

public enum StockMovementType
{
    Receipt = 0,            // Entrée de stock
    Issue = 1,              // Sortie de stock
    PhysicalAdjustment = 2  // Ajustement inventaire physique
}

public sealed class StockMovement : IDossierScopedEntity
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid ExerciceId { get; set; }

    public Guid StockItemId { get; set; }
    public StockMovementType MovementType { get; set; }
    public DateTime MovementDate { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }

    public decimal RunningStockQuantity { get; set; }
    public decimal RunningCump { get; set; }

    public string Reference { get; set; } = string.Empty;
    public Guid? DocumentId { get; set; }

    // Navigation
    public StockItem StockItem { get; set; } = null!;
}
