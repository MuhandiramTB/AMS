using TAMS.Domain.Common;

namespace TAMS.Domain.Audit;

/// <summary>
/// Append-only, tamper-evident audit record. Created only by the persistence
/// interceptor; never updated or deleted (enforced by DB grants in Infrastructure).
/// (FR-AUD-001/002, 04 Audit.AuditEntry, 06 §11.)
/// </summary>
public sealed class AuditEntry : Entity
{
    private AuditEntry()
    {
    }

    public AuditEntry(
        long? actorUserId,
        string action,
        string entityName,
        string entityId,
        string? oldValuesJson,
        string? newValuesJson,
        Guid correlationId,
        DateTime occurredAtUtc)
    {
        ActorUserId = actorUserId;
        Action = action;
        EntityName = entityName;
        EntityId = entityId;
        OldValuesJson = oldValuesJson;
        NewValuesJson = newValuesJson;
        CorrelationId = correlationId;
        OccurredAtUtc = occurredAtUtc;
    }

    public long? ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityName { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string? OldValuesJson { get; private set; }
    public string? NewValuesJson { get; private set; }
    public Guid CorrelationId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
}
