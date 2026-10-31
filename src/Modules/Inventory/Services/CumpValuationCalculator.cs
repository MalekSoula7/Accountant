namespace ComptaShield.Inventory.Services;

public sealed record StockState(
    decimal TotalQuantity,
    decimal UnitCost,
    decimal TotalValuation
);

public sealed record ValuationMovementResult(
    decimal QuantityChange,
    decimal UnitCostApplied,
    decimal TotalAmount,
    StockState NewState
);

/// <summary>
/// Implements the Coût Unitaire Moyen Pondéré (CUMP) valuation method
/// recalculated after every receipt (Entrée de stock).
/// Issues (Sorties) are valued at the latest prevailing CUMP.
/// Enforces strict decimal arithmetic and MidpointRounding.ToEven.
/// </summary>
public sealed class CumpValuationCalculator
{
    /// <summary>
    /// Processes a receipt (Entrée de stock) and recalculates the weighted average unit cost.
    /// Formula: CUMP = (PrevValuation + NewQty * NewPrice) / (PrevQty + NewQty)
    /// </summary>
    public ValuationMovementResult ProcessReceipt(
        StockState currentState, 
        decimal receiptQuantity, 
        decimal unitPrice)
    {
        if (receiptQuantity <= 0m)
            throw new ArgumentOutOfRangeException(nameof(receiptQuantity), "Receipt quantity must be positive.");
        if (unitPrice < 0m)
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");

        var receiptTotal = decimal.Round(receiptQuantity * unitPrice, 4, MidpointRounding.ToEven);
        var newTotalQuantity = currentState.TotalQuantity + receiptQuantity;
        var newTotalValuation = decimal.Round(currentState.TotalValuation + receiptTotal, 4, MidpointRounding.ToEven);

        var newCump = newTotalQuantity > 0m
            ? decimal.Round(newTotalValuation / newTotalQuantity, 4, MidpointRounding.ToEven)
            : 0m;

        var newState = new StockState(newTotalQuantity, newCump, newTotalValuation);

        return new ValuationMovementResult(receiptQuantity, unitPrice, receiptTotal, newState);
    }

    /// <summary>
    /// Processes an issue (Sortie de stock) valued at the current prevailing CUMP.
    /// </summary>
    public ValuationMovementResult ProcessIssue(
        StockState currentState, 
        decimal issueQuantity)
    {
        if (issueQuantity <= 0m)
            throw new ArgumentOutOfRangeException(nameof(issueQuantity), "Issue quantity must be positive.");
        if (issueQuantity > currentState.TotalQuantity)
            throw new InvalidOperationException(
                $"Stock shortage: Attempted to issue {issueQuantity} units, but only {currentState.TotalQuantity} units are available.");

        var unitCost = currentState.UnitCost;
        var totalIssueAmount = decimal.Round(issueQuantity * unitCost, 4, MidpointRounding.ToEven);

        var newTotalQuantity = currentState.TotalQuantity - issueQuantity;
        var newTotalValuation = decimal.Round(currentState.TotalValuation - totalIssueAmount, 4, MidpointRounding.ToEven);

        // Keep unit cost steady, or 0 if empty
        var remainingCump = newTotalQuantity > 0m ? unitCost : 0m;
        if (newTotalQuantity == 0m) newTotalValuation = 0m;

        var newState = new StockState(newTotalQuantity, remainingCump, newTotalValuation);

        return new ValuationMovementResult(-issueQuantity, unitCost, totalIssueAmount, newState);
    }
}
