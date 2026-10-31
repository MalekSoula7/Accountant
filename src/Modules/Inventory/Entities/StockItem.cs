using ComptaShield.SharedKernel.MultiTenancy;

namespace ComptaShield.Inventory.Entities;

public enum StockValuationMethod
{
    CUMP = 0, // Coût Unitaire Moyen Pondéré
    FIFO = 1  // First In, First Out
}

public sealed class StockItem : IDossierScopedEntity
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid ExerciceId { get; set; }

    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = "U";
    public StockValuationMethod ValuationMethod { get; set; } = StockValuationMethod.CUMP;

    public decimal TheoreticalQuantity { get; set; }
    public decimal CurrentUnitCost { get; set; }
    public decimal TotalValuation { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<StockMovement> Movements { get; set; } = [];
}
