using ComptaShield.Ingestion.Entities;
using ComptaShield.Ingestion.Models;

namespace ComptaShield.Ingestion.Validators;

public sealed record ValidationResult(
    bool IsValid,
    DocumentStatus TargetStatus,
    List<string> Discrepancies,
    decimal? LowestConfidenceFieldScore
);

/// <summary>
/// Enforces financial cross-sum arithmetic invariants and OCR confidence thresholds.
/// Any OCR document with confidence < 0.95 or failed math routes to RequiresHumanReview.
/// </summary>
public sealed class DocumentMathAndConfidenceValidator
{
    public const decimal MinimumConfidenceThreshold = 0.95m;
    private const decimal RoundingTolerance = 0.005m; // tolerance for 2-4 decimal rounding discrepancy

    public ValidationResult Validate(ExtractedInvoiceData invoiceData)
    {
        ArgumentNullException.ThrowIfNull(invoiceData);

        var discrepancies = new List<string>();
        decimal? lowestConfidence = null;

        // 1. Confidence Verification (for OCR documents)
        if (invoiceData.OverallConfidence.HasValue)
        {
            lowestConfidence = invoiceData.OverallConfidence.Value;
            if (invoiceData.OverallConfidence.Value < MinimumConfidenceThreshold)
            {
                discrepancies.Add(
                    $"Overall OCR confidence ({invoiceData.OverallConfidence.Value:P2}) is below required {MinimumConfidenceThreshold:P2} threshold.");
            }
        }

        if (invoiceData.OcrFields != null && invoiceData.OcrFields.Count > 0)
        {
            foreach (var field in invoiceData.OcrFields)
            {
                if (lowestConfidence == null || field.Confidence < lowestConfidence.Value)
                {
                    lowestConfidence = field.Confidence;
                }

                if (field.Confidence < MinimumConfidenceThreshold)
                {
                    discrepancies.Add(
                        $"Field '{field.FieldName}' confidence ({field.Confidence:P2}) is below required {MinimumConfidenceThreshold:P2} threshold.");
                }
            }
        }

        // 2. Line Items Arithmetic Invariant: Sum(Qty * UnitPrice - Discount) == LineTotal
        if (invoiceData.Lines.Count > 0)
        {
            decimal computedSumHT = 0m;

            foreach (var line in invoiceData.Lines)
            {
                var expectedLineTotal = decimal.Round(
                    (line.Quantity * line.UnitPrice) - line.Discount, 
                    4, 
                    MidpointRounding.ToEven);

                if (line.LineTotal != 0m && Math.Abs(expectedLineTotal - line.LineTotal) > RoundingTolerance)
                {
                    discrepancies.Add(
                        $"Line {line.LineNumber} arithmetic mismatch: Qty({line.Quantity}) * UnitPrice({line.UnitPrice}) - Discount({line.Discount}) = {expectedLineTotal}, but extracted LineTotal is {line.LineTotal}.");
                }

                computedSumHT += line.LineTotal != 0m ? line.LineTotal : expectedLineTotal;
            }

            // Verify lines sum equals TotalHT
            if (invoiceData.TotalHT != 0m && Math.Abs(computedSumHT - invoiceData.TotalHT) > RoundingTolerance)
            {
                discrepancies.Add(
                    $"Sum of line totals ({computedSumHT}) does not match TotalHT ({invoiceData.TotalHT}).");
            }
        }

        // 3. Header Cross-Sum Gate: TotalHT + TotalTVA + TimbreFiscal == TotalTTC
        var expectedTotalTTC = decimal.Round(
            invoiceData.TotalHT + invoiceData.TotalTVA + invoiceData.TimbreFiscal, 
            4, 
            MidpointRounding.ToEven);

        if (Math.Abs(expectedTotalTTC - invoiceData.TotalTTC) > RoundingTolerance)
        {
            discrepancies.Add(
                $"Cross-sum failed: TotalHT({invoiceData.TotalHT}) + TotalTVA({invoiceData.TotalTVA}) + TimbreFiscal({invoiceData.TimbreFiscal}) = {expectedTotalTTC}, but extracted TotalTTC is {invoiceData.TotalTTC}.");
        }

        var isValid = discrepancies.Count == 0;
        var targetStatus = isValid ? DocumentStatus.ReadyForPosting : DocumentStatus.RequiresHumanReview;

        return new ValidationResult(isValid, targetStatus, discrepancies, lowestConfidence);
    }
}
