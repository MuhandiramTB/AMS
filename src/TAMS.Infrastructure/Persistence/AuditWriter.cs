using TAMS.Application.Common.Ports;
using TAMS.Domain.Audit;
using TAMS.Domain.Common;

namespace TAMS.Infrastructure.Persistence;

/// <summary>
/// Writes an explicit append-only audit entry for non-mutating actions (exports),
/// which the SaveChanges interceptor doesn't cover. Persists immediately so the
/// audit exists even though no business data changed. (FR-RPT-007, FR-AUD-001.)
/// </summary>
public sealed class AuditWriter : IAuditWriter
{
    private readonly TamsDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ICorrelationIdAccessor _correlation;

    public AuditWriter(TamsDbContext db, IClock clock, ICurrentUser currentUser, ICorrelationIdAccessor correlation)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _correlation = correlation;
    }

    public async Task RecordAsync(string action, string entityName, string entityId, CancellationToken cancellationToken = default)
    {
        var entry = new AuditEntry(
            _currentUser.UserId,
            action,
            entityName,
            entityId,
            oldValuesJson: null,
            newValuesJson: null,
            _correlation.CorrelationId,
            _clock.UtcNow);

        await _db.AuditEntries.AddAsync(entry, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
