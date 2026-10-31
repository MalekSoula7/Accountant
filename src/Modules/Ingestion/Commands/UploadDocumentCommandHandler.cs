using System.Security.Cryptography;
using ComptaShield.Ingestion.Entities;
using ComptaShield.Ingestion.Events;
using ComptaShield.Ingestion.Persistence;
using ComptaShield.Ingestion.Storage;
using ComptaShield.SharedKernel.MultiTenancy;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.Ingestion.Commands;

/// <summary>
/// Handles document upload with SHA-256 binary deduplication and transactional outbox.
/// </summary>
public sealed class UploadDocumentCommandHandler
{
    private readonly IngestionDbContext _dbContext;
    private readonly IWormStorageService _storageService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ITenantContext _tenantContext;

    public UploadDocumentCommandHandler(
        IngestionDbContext dbContext,
        IWormStorageService storageService,
        IPublishEndpoint publishEndpoint,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _storageService = storageService;
        _publishEndpoint = publishEndpoint;
        _tenantContext = tenantContext;
    }

    public async Task<UploadDocumentResult> HandleAsync(
        Stream fileStream, string originalFileName, CancellationToken ct = default)
    {
        // Compute SHA-256
        var sha256 = await ComputeSha256Async(fileStream, ct);
        fileStream.Position = 0;

        var extension = Path.GetExtension(originalFileName).TrimStart('.').ToLowerInvariant();

        // Check for duplicate in DB (scoped to CabinetId + DossierId)
        var existingDoc = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Sha256Hash == sha256, ct);

        if (existingDoc is not null)
        {
            return new UploadDocumentResult(false, existingDoc.Id, "Duplicate document: SHA-256 already exists.");
        }

        // Store in WORM vault
        var objectKey = await _storageService.StoreInboundFileAsync(
            _tenantContext.CabinetId, _tenantContext.DossierId, _tenantContext.ExerciceId,
            fileStream, sha256, extension, ct);

        if (objectKey is null)
        {
            return new UploadDocumentResult(false, null, "Duplicate detected in object storage.");
        }

        // Create document record
        var document = new IngestedDocument
        {
            Id = Guid.NewGuid(),
            OriginalFileName = originalFileName,
            FileExtension = extension,
            Sha256Hash = sha256,
            ObjectStorageKey = objectKey,
            FileSizeBytes = fileStream.Length,
            Status = DocumentStatus.Uploaded,
            Source = extension == "xml" ? DocumentSource.TeifXml : DocumentSource.ManualUpload,
            UploadedByUserId = _tenantContext.UserId,
            UploadedAt = DateTime.UtcNow
        };

        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync(ct);

        // Publish integration event (transactional outbox pattern)
        await _publishEndpoint.Publish(new DocumentUploadedIntegrationEvent
        {
            DocumentId = document.Id,
            CabinetId = _tenantContext.CabinetId,
            DossierId = _tenantContext.DossierId,
            ExerciceId = _tenantContext.ExerciceId,
            ObjectStorageKey = objectKey,
            FileExtension = extension,
            Sha256Hash = sha256,
            UploadedByUserId = _tenantContext.UserId,
            UploadedAt = document.UploadedAt
        }, ct);

        return new UploadDocumentResult(true, document.Id, null);
    }

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken ct)
    {
        var hashBytes = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexStringLower(hashBytes);
    }
}

public sealed record UploadDocumentResult(bool Success, Guid? DocumentId, string? ErrorMessage);
