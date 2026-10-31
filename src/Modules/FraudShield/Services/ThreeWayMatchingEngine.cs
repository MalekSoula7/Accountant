namespace ComptaShield.FraudShield.Services;

public sealed record PurchaseOrderLine(
    string ItemCode,
    string Description,
    decimal Quantity,
    decimal UnitPrice
);

public sealed record PurchaseOrder(
    string PoNumber,
    DateTime Date,
    string SupplierMatriculeFiscal,
    List<PurchaseOrderLine> Lines
);

public sealed record DeliveryNoteLine(
    string ItemCode,
    decimal QuantityReceived
);

public sealed record DeliveryNote(
    string BlNumber,
    string PoNumber,
    DateTime ReceptionDate,
    List<DeliveryNoteLine> Lines
);

public sealed record InvoiceMatchLine(
    string ItemCode,
    decimal QuantityInvoiced,
    decimal UnitPriceInvoiced
);

public sealed record InvoiceMatchInput(
    string InvoiceNumber,
    string? PoNumber,
    string? BlNumber,
    string SupplierMatriculeFiscal,
    DateTime InvoiceDate,
    decimal TotalHT,
    decimal TotalTTC,
    List<InvoiceMatchLine> Lines
);

public sealed record BankStatementLineMatch(
    string TransactionId,
    DateTime Date,
    decimal AmountDebit,
    string Beneficiary,
    string Reference
);

public enum MatchDiscrepancyType
{
    None = 0,
    PhantomDelivery = 1,          // Invoiced items never received in Delivery Note
    UnitPriceInflation = 2,       // Invoiced price exceeds agreed PO price
    QuantityDiscrepancy = 3,      // Quantities mismatch across PO, BL, and Invoice
    MissingPurchaseOrder = 4,     // Invoice has no corresponding PO
    MissingDeliveryNote = 5,      // Invoice has no corresponding Delivery Note
    BankPaymentMismatch = 6,      // Paid amount on bank statement differs from Invoice TTC
    UnmatchedBankPayment = 7      // Payment executed with no matching invoice
}

public sealed record MatchDiscrepancy(
    MatchDiscrepancyType Type,
    string ItemCode,
    string Description,
    decimal ExpectedValue,
    decimal ActualValue,
    decimal FinancialImpact
);

public sealed record ThreeWayMatchResult(
    bool IsFullMatch,
    decimal RiskScore,
    decimal TotalFinancialImpact,
    List<MatchDiscrepancy> Discrepancies
);

/// <summary>
/// Reconciles Purchase Order (BC) <-> Delivery Note (BL) <-> Supplier Invoice (Facture) <-> Bank Statement (Relevé).
/// Detects phantom deliveries, unit price inflation, quantity mismatches, and overpayments.
/// </summary>
public sealed class ThreeWayMatchingEngine
{
    private const decimal PriceTolerance = 0.005m;
    private const decimal QuantityTolerance = 0.001m;

    public ThreeWayMatchResult Reconcile(
        PurchaseOrder? po,
        DeliveryNote? bl,
        InvoiceMatchInput invoice,
        BankStatementLineMatch? bankLine = null)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var discrepancies = new List<MatchDiscrepancy>();
        decimal riskScore = 0m;
        decimal totalFinancialImpact = 0m;

        // 1. Check Missing PO
        if (po == null)
        {
            discrepancies.Add(new MatchDiscrepancy(
                MatchDiscrepancyType.MissingPurchaseOrder,
                string.Empty,
                $"Invoice '{invoice.InvoiceNumber}' has no matching Purchase Order.",
                0m,
                invoice.TotalTTC,
                invoice.TotalTTC));

            riskScore = Math.Max(riskScore, 0.70m);
            totalFinancialImpact += invoice.TotalTTC;
        }

        // 2. Check Missing BL (Goods receipt)
        if (bl == null)
        {
            discrepancies.Add(new MatchDiscrepancy(
                MatchDiscrepancyType.MissingDeliveryNote,
                string.Empty,
                $"Invoice '{invoice.InvoiceNumber}' has no delivery receipt note (potential phantom invoice).",
                0m,
                invoice.TotalTTC,
                invoice.TotalTTC));

            riskScore = Math.Max(riskScore, 0.85m);
            totalFinancialImpact += invoice.TotalTTC;
        }

        // 3. Line-by-Line 3-Way Reconciliation
        if (po != null && bl != null)
        {
            var poLineDict = po.Lines.ToDictionary(l => l.ItemCode.ToUpperInvariant(), l => l);
            var blLineDict = bl.Lines.ToDictionary(l => l.ItemCode.ToUpperInvariant(), l => l);

            foreach (var invLine in invoice.Lines)
            {
                var itemKey = invLine.ItemCode.ToUpperInvariant();

                poLineDict.TryGetValue(itemKey, out var poLine);
                blLineDict.TryGetValue(itemKey, out var blLine);

                // A. Phantom Delivery Check: Invoiced quantity vs Received quantity
                var receivedQty = blLine?.QuantityReceived ?? 0m;
                if (invLine.QuantityInvoiced > receivedQty + QuantityTolerance)
                {
                    var excessQty = invLine.QuantityInvoiced - receivedQty;
                    var impact = excessQty * invLine.UnitPriceInvoiced;

                    discrepancies.Add(new MatchDiscrepancy(
                        MatchDiscrepancyType.PhantomDelivery,
                        invLine.ItemCode,
                        $"Phantom delivery: Invoiced {invLine.QuantityInvoiced} units of '{invLine.ItemCode}', but only {receivedQty} units were physically received.",
                        receivedQty,
                        invLine.QuantityInvoiced,
                        impact));

                    riskScore = Math.Max(riskScore, 0.90m);
                    totalFinancialImpact += impact;
                }

                // B. Unit Price Inflation Check: Invoiced unit price vs PO agreed unit price
                if (poLine != null)
                {
                    if (invLine.UnitPriceInvoiced > poLine.UnitPrice + PriceTolerance)
                    {
                        var unitPriceDiff = invLine.UnitPriceInvoiced - poLine.UnitPrice;
                        var impact = unitPriceDiff * invLine.QuantityInvoiced;

                        discrepancies.Add(new MatchDiscrepancy(
                            MatchDiscrepancyType.UnitPriceInflation,
                            invLine.ItemCode,
                            $"Unit price inflation on '{invLine.ItemCode}': Agreed PO price = {poLine.UnitPrice:N4}, Invoiced price = {invLine.UnitPriceInvoiced:N4} (+{unitPriceDiff:N4}/unit).",
                            poLine.UnitPrice,
                            invLine.UnitPriceInvoiced,
                            impact));

                        riskScore = Math.Max(riskScore, 0.80m);
                        totalFinancialImpact += impact;
                    }
                }
            }
        }

        // 4. Bank Statement Line Reconciliation
        if (bankLine != null)
        {
            var paymentDiff = bankLine.AmountDebit - invoice.TotalTTC;
            if (Math.Abs(paymentDiff) > 0.01m)
            {
                var discrepancyType = MatchDiscrepancyType.BankPaymentMismatch;
                var impact = Math.Abs(paymentDiff);

                discrepancies.Add(new MatchDiscrepancy(
                    discrepancyType,
                    string.Empty,
                    $"Bank debit ({bankLine.AmountDebit:N4}) does not match invoice TTC ({invoice.TotalTTC:N4}). Difference: {paymentDiff:N4}",
                    invoice.TotalTTC,
                    bankLine.AmountDebit,
                    impact));

                riskScore = Math.Max(riskScore, paymentDiff > 0 ? 0.85m : 0.60m);
                totalFinancialImpact += impact;
            }
        }

        var isFullMatch = discrepancies.Count == 0;

        return new ThreeWayMatchResult(
            isFullMatch,
            decimal.Round(riskScore, 2, MidpointRounding.ToEven),
            decimal.Round(totalFinancialImpact, 4, MidpointRounding.ToEven),
            discrepancies
        );
    }
}
