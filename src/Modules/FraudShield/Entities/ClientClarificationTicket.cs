using ComptaShield.SharedKernel.MultiTenancy;

namespace ComptaShield.FraudShield.Entities;

public enum ClarificationTicketStatus
{
    Pending = 0,
    Submitted = 1,
    ReviewedByAuditor = 2,
    Expired = 3
}

/// <summary>
/// Magic-link clarification ticket allowing ClientGuest users to provide explanations
/// and upload supporting documents for flagged invoices or anomalies.
/// Scoped to CabinetId, DossierId, ExerciceId.
/// </summary>
public sealed class ClientClarificationTicket : IDossierScopedEntity
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid ExerciceId { get; set; }

    public Guid FraudAlertId { get; set; }
    public Guid? DocumentId { get; set; }

    public string MagicToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public ClarificationTicketStatus Status { get; set; } = ClarificationTicketStatus.Pending;

    public string QuestionPrompt { get; set; } = string.Empty;
    public string? ClientExplanation { get; set; }
    public string? SupportingDocumentKey { get; set; }
    public DateTime? SubmittedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
