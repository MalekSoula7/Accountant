using System.Security.Cryptography;
using System.Text;
using ComptaShield.Audit.Entities;

namespace ComptaShield.Audit.Services;

public sealed record DeliverableSigningResult(
    AuditDeliverable Deliverable,
    string StorageKey,
    string Sha256Hash,
    string HmacSignature
);

/// <summary>
/// Generates cryptographically hashed deliverables (Bilan, PV d'Inventaire, Audit Report)
/// stamped for the Tier-2 Deliverables Vault with immutable Object Lock.
/// </summary>
public sealed class SignedDeliverableGenerator
{
    private static readonly byte[] DefaultSigningSecret = Encoding.UTF8.GetBytes("ComptaShield_Deliverable_Vault_Secret_2026!");

    public async Task<DeliverableSigningResult> GenerateAndSignDeliverableAsync(
        Guid cabinetId,
        Guid dossierId,
        Guid exerciceId,
        string docType,
        string version,
        Stream documentStream,
        Guid signedByUserId,
        byte[]? hmacSecret = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(documentStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(docType);

        // 1. Compute SHA-256 of document stream
        documentStream.Position = 0;
        var shaBytes = await SHA256.HashDataAsync(documentStream, ct);
        var sha256Hex = Convert.ToHexStringLower(shaBytes);
        documentStream.Position = 0;

        // 2. Compute HMAC-SHA256 signature
        var secret = hmacSecret ?? DefaultSigningSecret;
        using var hmac = new HMACSHA256(secret);
        var signaturePayload = Encoding.UTF8.GetBytes($"{cabinetId}:{dossierId}:{exerciceId}:{docType}:{version}:{sha256Hex}:{signedByUserId}");
        var signatureBytes = hmac.ComputeHash(signaturePayload);
        var signatureHex = Convert.ToHexStringLower(signatureBytes);

        // 3. Build WORM storage key
        var normalizedDocType = docType.Trim().ToLowerInvariant();
        var normalizedVersion = version.Trim().ToLowerInvariant();
        var storageKey = $"{cabinetId}/{dossierId}/{exerciceId}/deliverables/{normalizedDocType}_{normalizedVersion}_{sha256Hex}.pdf";

        var deliverable = new AuditDeliverable
        {
            Id = Guid.NewGuid(),
            CabinetId = cabinetId,
            DossierId = dossierId,
            ExerciceId = exerciceId,
            DeliverableType = docType,
            Version = version,
            Sha256Hash = sha256Hex,
            StorageKey = storageKey,
            SignedByUserId = signedByUserId,
            SignedAt = DateTime.UtcNow,
            CryptographicSignature = signatureHex
        };

        return new DeliverableSigningResult(
            deliverable,
            storageKey,
            sha256Hex,
            signatureHex
        );
    }
}
