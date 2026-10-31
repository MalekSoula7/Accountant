using System.Text.Json;
using ComptaShield.Ingestion.Entities;
using ComptaShield.Ingestion.Events;
using ComptaShield.Ingestion.Models;
using ComptaShield.Ingestion.Persistence;
using ComptaShield.Ingestion.Storage;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ComptaShield.Ingestion.Workers;

/// <summary>
/// MassTransit consumer that processes uploaded documents.
/// For XML files: uses TeifXmlInvoiceParser.
/// For other files: simulates OCR extraction with JSONB fields,
/// bounding box coordinates, and per-field confidence scores.
/// </summary>
public sealed class OcrExtractionWorker : IConsumer<DocumentUploadedIntegrationEvent>
{
    private readonly IngestionDbContext _dbContext;
    private readonly IWormStorageService _storageService;
    private readonly Parsers.TeifXmlInvoiceParser _teifParser;
    private readonly ILogger<OcrExtractionWorker> _logger;

    public OcrExtractionWorker(
        IngestionDbContext dbContext,
        IWormStorageService storageService,
        Parsers.TeifXmlInvoiceParser teifParser,
        ILogger<OcrExtractionWorker> logger)
    {
        _dbContext = dbContext;
        _storageService = storageService;
        _teifParser = teifParser;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DocumentUploadedIntegrationEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing document {DocumentId} ({Extension})",
            message.DocumentId, message.FileExtension);

        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == message.DocumentId, context.CancellationToken);

        if (document is null)
        {
            _logger.LogWarning("Document {DocumentId} not found", message.DocumentId);
            return;
        }

        document.Status = DocumentStatus.Processing;
        await _dbContext.SaveChangesAsync(context.CancellationToken);

        try
        {
            var fileStream = await _storageService.GetFileAsync(message.ObjectStorageKey, context.CancellationToken);

            ExtractedInvoiceData extractedData;

            if (string.Equals(message.FileExtension, "xml", StringComparison.OrdinalIgnoreCase))
            {
                // TEIF XML parsing — structured, high confidence
                extractedData = _teifParser.Parse(fileStream);
                document.Source = DocumentSource.TeifXml;
                document.OcrConfidence = null; // TEIF XML doesn't have OCR confidence
            }
            else
            {
                // OCR extraction — simulate structured extraction with confidence scores
                // In production, this would call an external OCR service (Google Vision, Azure Document Intelligence, etc.)
                extractedData = SimulateOcrExtraction(fileStream);
                document.Source = DocumentSource.OcrScanned;
                document.OcrConfidence = extractedData.OverallConfidence;
            }

            document.ExtractedDataJson = JsonSerializer.Serialize(extractedData,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            document.ProcessedAt = DateTime.UtcNow;

            // Status will be set by DocumentMathAndConfidenceValidator downstream
            document.Status = DocumentStatus.Processing;
            await _dbContext.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation("Document {DocumentId} extraction complete", message.DocumentId);
        }
        catch (Exception ex)
        {
            document.Status = DocumentStatus.RequiresHumanReview;
            document.ProcessingError = ex.Message;
            await _dbContext.SaveChangesAsync(context.CancellationToken);
            _logger.LogError(ex, "Failed to process document {DocumentId}", message.DocumentId);
        }
    }

    /// <summary>
    /// Simulates OCR extraction. In production, replace with actual OCR service integration.
    /// Returns extracted data with bounding boxes and confidence scores.
    /// </summary>
    private static ExtractedInvoiceData SimulateOcrExtraction(Stream fileStream)
    {
        // This is a placeholder that returns a minimal result.
        // Real implementation would call an OCR API and map results.
        return new ExtractedInvoiceData
        {
            OverallConfidence = 0.85m, // Default simulated — below 0.95 threshold
            OcrFields =
            [
                new OcrFieldResult
                {
                    FieldName = "InvoiceNumber",
                    Value = "PLACEHOLDER",
                    Confidence = 0.90m,
                    BoundingBox = [1, 100, 50, 200, 30]
                },
                new OcrFieldResult
                {
                    FieldName = "TotalTTC",
                    Value = "0.00",
                    Confidence = 0.80m,
                    BoundingBox = [1, 400, 600, 150, 25]
                }
            ]
        };
    }
}
