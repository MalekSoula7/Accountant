using System.Security.Cryptography;
using ComptaShield.FraudShield.Entities;
using ComptaShield.FraudShield.Persistence;
using ComptaShield.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.FraudShield.Services;

public sealed record JournalLineDto(
    string AccountNumber,
    string Description,
    decimal Debit,
    decimal Credit
);

public sealed record ReversingEntryProposal(
    Guid ReversesEntryId,
    string EntryNumber,
    DateTime Date,
    string Description,
    List<JournalLineDto> Lines,
    decimal TotalDebit,
    decimal TotalCredit
);

/// <summary>
/// Handles fraud quarantine workflows, magic-link client clarification tickets,
/// and auto-generates balanced double-entry reversing journal entries (Contrepassation).
/// </summary>
public sealed class FraudRectificationService
{
    private readonly FraudDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public FraudRectificationService(FraudDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Quarantines an alert and its associated document from Bilan / Ledger inclusion.
    /// </summary>
    public async Task<bool> QuarantineDocumentAsync(
        Guid alertId, 
        string reason, 
        CancellationToken ct = default)
    {
        var alert = await _dbContext.Alerts.FirstOrDefaultAsync(a => a.Id == alertId, ct);
        if (alert == null) return false;

        alert.Status = FraudAlertStatus.Quarantined;
        alert.ResolutionNotes = $"Quarantined by auditor: {reason}";
        alert.ResolvedAt = DateTime.UtcNow;
        alert.ResolvedByUserId = _tenantContext.UserId;

        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Generates a secure magic-link ticket for the external client (ClientGuest) to upload justification.
    /// </summary>
    public async Task<ClientClarificationTicket> GenerateClientClarificationTicketAsync(
        Guid alertId,
        string questionPrompt,
        TimeSpan? validity = null,
        CancellationToken ct = default)
    {
        var alert = await _dbContext.Alerts.FirstOrDefaultAsync(a => a.Id == alertId, ct)
            ?? throw new InvalidOperationException($"FraudAlert {alertId} not found.");

        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        var token = Convert.ToHexStringLower(randomBytes);

        var ticket = new ClientClarificationTicket
        {
            Id = Guid.NewGuid(),
            FraudAlertId = alert.Id,
            DocumentId = alert.DocumentId,
            MagicToken = token,
            QuestionPrompt = questionPrompt,
            ExpiresAt = DateTime.UtcNow.Add(validity ?? TimeSpan.FromDays(7)),
            Status = ClarificationTicketStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        alert.Status = FraudAlertStatus.ClarificationRequested;

        _dbContext.ClarificationTickets.Add(ticket);
        await _dbContext.SaveChangesAsync(ct);

        return ticket;
    }

    /// <summary>
    /// Auto-generates an immutable balanced reversing entry (Écriture de Contrepassation)
    /// for a fraudulent or erroneous posted entry.
    /// Invariant: Sum(Lines.Debit) == Sum(Lines.Credit).
    /// </summary>
    public ReversingEntryProposal CreateReversingEntryProposal(
        Guid originalEntryId,
        string originalEntryNumber,
        IReadOnlyList<JournalLineDto> originalLines,
        string rectificationReason)
    {
        ArgumentNullException.ThrowIfNull(originalLines);
        if (originalLines.Count == 0)
            throw new ArgumentException("Cannot reverse an entry with no lines.", nameof(originalLines));

        var reversingLines = new List<JournalLineDto>();
        decimal sumDebit = 0m;
        decimal sumCredit = 0m;

        foreach (var line in originalLines)
        {
            // Swap Debit and Credit exactly
            var revDebit = line.Credit;
            var revCredit = line.Debit;

            sumDebit += revDebit;
            sumCredit += revCredit;

            reversingLines.Add(new JournalLineDto(
                line.AccountNumber,
                $"[ANNULATION] {line.Description}",
                revDebit,
                revCredit
            ));
        }

        // Verify fundamental double-entry invariant
        if (decimal.Round(sumDebit, 4, MidpointRounding.ToEven) != decimal.Round(sumCredit, 4, MidpointRounding.ToEven))
        {
            throw new InvalidOperationException(
                $"Unbalanced reversing entry: Debit ({sumDebit:N4}) != Credit ({sumCredit:N4}). Double-entry invariant violated.");
        }

        return new ReversingEntryProposal(
            originalEntryId,
            $"REV-{originalEntryNumber}",
            DateTime.UtcNow,
            $"Contrepassation régularisation: {rectificationReason} (Réf: {originalEntryNumber})",
            reversingLines,
            decimal.Round(sumDebit, 4, MidpointRounding.ToEven),
            decimal.Round(sumCredit, 4, MidpointRounding.ToEven)
        );
    }
}
