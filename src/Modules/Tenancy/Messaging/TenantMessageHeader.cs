namespace ComptaShield.Tenancy.Messaging;

/// <summary>
/// Envelope header carried on all MassTransit messages to propagate tenant context
/// from HTTP requests to async background workers (OCR, Fraud, etc.).
/// </summary>
public sealed record TenantMessageHeader
{
    public const string HeaderKey = "X-Tenant-Context";

    public Guid CabinetId { get; init; }
    public Guid DossierId { get; init; }
    public Guid ExerciceId { get; init; }
    public Guid InitiatedByUserId { get; init; }
    public string Role { get; init; } = string.Empty;
}
