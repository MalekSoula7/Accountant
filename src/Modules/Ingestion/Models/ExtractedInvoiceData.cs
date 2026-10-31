namespace ComptaShield.Ingestion.Models;

/// <summary>
/// Structured data extracted from a TEIF XML e-invoice or OCR scan.
/// </summary>
public sealed class ExtractedInvoiceData
{
    public string? SupplierName { get; set; }
    public string? SupplierMatriculeFiscal { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }

    public List<ExtractedInvoiceLine> Lines { get; set; } = [];

    public decimal TotalHT { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TimbreFiscal { get; set; }
    public decimal TotalTTC { get; set; }

    public List<TvaBreakdownLine> TvaBreakdown { get; set; } = [];

    /// <summary>Digital signature metadata from TEIF XML (null for OCR).</summary>
    public string? DigitalSignatureInfo { get; set; }

    /// <summary>Overall confidence score for OCR-extracted documents (0.00-1.00). Null for TEIF XML.</summary>
    public decimal? OverallConfidence { get; set; }

    /// <summary>Per-field confidence scores and bounding boxes for OCR documents.</summary>
    public List<OcrFieldResult>? OcrFields { get; set; }
}

public sealed class ExtractedInvoiceLine
{
    public int LineNumber { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TvaRate { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class TvaBreakdownLine
{
    public decimal Rate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
}

public sealed class OcrFieldResult
{
    public string FieldName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    /// <summary>Bounding box: [page, x, y, width, height]</summary>
    public decimal[] BoundingBox { get; set; } = [];
}
