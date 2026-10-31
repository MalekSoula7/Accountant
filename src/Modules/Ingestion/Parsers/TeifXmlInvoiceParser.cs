using System.Globalization;
using System.Xml.Linq;
using ComptaShield.Ingestion.Models;

namespace ComptaShield.Ingestion.Parsers;

/// <summary>
/// Parses structured TEIF XML e-invoice documents.
/// Extracts supplier info, line items, TVA breakdown, totals, and digital signature metadata.
/// </summary>
public sealed class TeifXmlInvoiceParser
{
    /// <summary>
    /// Parses a TEIF XML stream and returns structured invoice data.
    /// </summary>
    public ExtractedInvoiceData Parse(Stream xmlStream)
    {
        var doc = XDocument.Load(xmlStream);
        var root = doc.Root ?? throw new InvalidOperationException("Empty XML document.");

        var result = new ExtractedInvoiceData
        {
            SupplierName = GetElementValue(root, "SupplierName"),
            SupplierMatriculeFiscal = GetElementValue(root, "SupplierMatriculeFiscal")
                                      ?? GetElementValue(root, "MatriculeFiscal"),
            InvoiceNumber = GetElementValue(root, "InvoiceNumber")
                            ?? GetElementValue(root, "NumeroFacture"),
            InvoiceDate = ParseDate(GetElementValue(root, "InvoiceDate")
                                    ?? GetElementValue(root, "DateFacture")),
            DueDate = ParseDate(GetElementValue(root, "DueDate")
                                ?? GetElementValue(root, "DateEcheance")),
            DigitalSignatureInfo = GetElementValue(root, "DigitalSignature")
                                   ?? GetElementValue(root, "Signature"),
            OverallConfidence = null // TEIF XML is structured — no OCR confidence
        };

        // Parse line items
        var linesElement = root.Descendants()
            .FirstOrDefault(e => e.Name.LocalName is "Lines" or "LignesFacture" or "InvoiceLines");

        if (linesElement is not null)
        {
            var lineNumber = 1;
            foreach (var lineEl in linesElement.Elements())
            {
                result.Lines.Add(new ExtractedInvoiceLine
                {
                    LineNumber = lineNumber++,
                    Description = GetElementValue(lineEl, "Description")
                                  ?? GetElementValue(lineEl, "Designation"),
                    Quantity = ParseDecimal(GetElementValue(lineEl, "Quantity")
                                            ?? GetElementValue(lineEl, "Quantite")),
                    UnitPrice = ParseDecimal(GetElementValue(lineEl, "UnitPrice")
                                             ?? GetElementValue(lineEl, "PrixUnitaire")),
                    Discount = ParseDecimal(GetElementValue(lineEl, "Discount")
                                            ?? GetElementValue(lineEl, "Remise")),
                    TvaRate = ParseDecimal(GetElementValue(lineEl, "TvaRate")
                                           ?? GetElementValue(lineEl, "TauxTVA")),
                    LineTotal = ParseDecimal(GetElementValue(lineEl, "LineTotal")
                                             ?? GetElementValue(lineEl, "MontantLigne"))
                });
            }
        }

        // Parse totals
        result.TotalHT = ParseDecimal(GetElementValue(root, "TotalHT")
                                       ?? GetElementValue(root, "TotalExclTax"));
        result.TotalTVA = ParseDecimal(GetElementValue(root, "TotalTVA")
                                        ?? GetElementValue(root, "TotalTax"));
        result.TimbreFiscal = ParseDecimal(GetElementValue(root, "TimbreFiscal")
                                            ?? GetElementValue(root, "FiscalStamp"));
        result.TotalTTC = ParseDecimal(GetElementValue(root, "TotalTTC")
                                        ?? GetElementValue(root, "TotalInclTax"));

        // Parse TVA breakdown
        var tvaElement = root.Descendants()
            .FirstOrDefault(e => e.Name.LocalName is "TvaBreakdown" or "VentilationTVA" or "TaxBreakdown");

        if (tvaElement is not null)
        {
            foreach (var tvaLine in tvaElement.Elements())
            {
                result.TvaBreakdown.Add(new TvaBreakdownLine
                {
                    Rate = ParseDecimal(GetElementValue(tvaLine, "Rate")
                                        ?? GetElementValue(tvaLine, "Taux")),
                    TaxableAmount = ParseDecimal(GetElementValue(tvaLine, "TaxableAmount")
                                                  ?? GetElementValue(tvaLine, "BaseImposable")),
                    TaxAmount = ParseDecimal(GetElementValue(tvaLine, "TaxAmount")
                                             ?? GetElementValue(tvaLine, "MontantTVA"))
                });
            }
        }

        return result;
    }

    private static string? GetElementValue(XElement parent, string localName)
    {
        var element = parent.Descendants()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));
        var value = element?.Value?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static decimal ParseDecimal(string? value)
        => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0m;

    private static DateTime? ParseDate(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) ? result : null;
}
