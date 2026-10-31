namespace ComptaShield.Inventory.Services;

public sealed record FifoLot(
    Guid LotId,
    DateTime ReceiptDate,
    decimal Quantity,
    decimal UnitPrice
)
{
    public decimal TotalAmount => decimal.Round(Quantity * UnitPrice, 4, MidpointRounding.ToEven);
}

public sealed record FifoIssueConsumption(
    Guid LotId,
    decimal QuantityConsumed,
    decimal UnitPrice,
    decimal TotalCost
);

public sealed record FifoIssueResult(
    decimal TotalQuantityIssued,
    decimal TotalCostOfIssue,
    List<FifoIssueConsumption> Consumptions,
    List<FifoLot> RemainingLots,
    decimal RemainingTotalQuantity,
    decimal RemainingTotalValuation
);

/// <summary>
/// Implements First-In, First-Out (FIFO) inventory lot tracking and valuation.
/// Issues consume the oldest receipt lots first. Remaining inventory is valued at latest lot prices.
/// </summary>
public sealed class FifoValuationCalculator
{
    public List<FifoLot> ProcessReceipt(
        IEnumerable<FifoLot> currentLots,
        Guid lotId,
        DateTime receiptDate,
        decimal quantity,
        decimal unitPrice)
    {
        if (quantity <= 0m)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Receipt quantity must be positive.");
        if (unitPrice < 0m)
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");

        var lots = currentLots.OrderBy(l => l.ReceiptDate).ToList();
        lots.Add(new FifoLot(lotId, receiptDate, quantity, unitPrice));
        return lots;
    }

    public FifoIssueResult ProcessIssue(
        IEnumerable<FifoLot> currentLots,
        decimal requestedQuantity)
    {
        if (requestedQuantity <= 0m)
            throw new ArgumentOutOfRangeException(nameof(requestedQuantity), "Issue quantity must be positive.");

        var sortedLots = currentLots.OrderBy(l => l.ReceiptDate).ToList();
        var availableQty = sortedLots.Sum(l => l.Quantity);

        if (requestedQuantity > availableQty)
            throw new InvalidOperationException(
                $"Stock shortage: Requested {requestedQuantity} units, but only {availableQty} units available in FIFO lots.");

        var consumptions = new List<FifoIssueConsumption>();
        var remainingLots = new List<FifoLot>();
        decimal quantityNeeded = requestedQuantity;
        decimal totalCost = 0m;

        foreach (var lot in sortedLots)
        {
            if (quantityNeeded <= 0m)
            {
                // Unconsumed lot remains intact
                remainingLots.Add(lot);
                continue;
            }

            if (lot.Quantity <= quantityNeeded)
            {
                // Lot fully consumed
                var cost = decimal.Round(lot.Quantity * lot.UnitPrice, 4, MidpointRounding.ToEven);
                consumptions.Add(new FifoIssueConsumption(lot.LotId, lot.Quantity, lot.UnitPrice, cost));
                totalCost += cost;
                quantityNeeded -= lot.Quantity;
            }
            else
            {
                // Lot partially consumed
                var cost = decimal.Round(quantityNeeded * lot.UnitPrice, 4, MidpointRounding.ToEven);
                consumptions.Add(new FifoIssueConsumption(lot.LotId, quantityNeeded, lot.UnitPrice, cost));
                totalCost += cost;

                var leftoverQty = lot.Quantity - quantityNeeded;
                remainingLots.Add(lot with { Quantity = leftoverQty });
                quantityNeeded = 0m;
            }
        }

        totalCost = decimal.Round(totalCost, 4, MidpointRounding.ToEven);
        var remainingQty = remainingLots.Sum(l => l.Quantity);
        var remainingValuation = decimal.Round(remainingLots.Sum(l => l.TotalAmount), 4, MidpointRounding.ToEven);

        return new FifoIssueResult(
            requestedQuantity,
            totalCost,
            consumptions,
            remainingLots,
            remainingQty,
            remainingValuation
        );
    }
}
