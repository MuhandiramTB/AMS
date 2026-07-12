using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Audit;
using TAMS.Domain.Common;

namespace TAMS.Infrastructure.Persistence;

/// <summary>
/// Builds the audit trail for a save. Stamps the standard audit columns on
/// mutable entities and produces the append-only AuditEntry rows — so no code
/// path can bypass the audit trail. (FR-AUD-001, BRULE-10, ADR-007/010, 04 §10.)
///
/// This is invoked by <see cref="TamsDbContext"/>, which persists the business
/// data and these audit rows in a SINGLE transaction, guaranteeing that a change
/// and its audit entry commit atomically (no committed change without its audit).
/// </summary>
public sealed class AuditTrailBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ICorrelationIdAccessor _correlation;

    public AuditTrailBuilder(IClock clock, ICurrentUser currentUser, ICorrelationIdAccessor correlation)
    {
        _clock = clock;
        _currentUser = currentUser;
        _correlation = correlation;
    }

    /// <summary>
    /// Stamps audit columns and captures the audited changes. Call this BEFORE
    /// saving business data. Returns the captured changes to be turned into
    /// AuditEntry rows after IDs are assigned.
    /// </summary>
    public IReadOnlyList<CapturedChange> StampAndCapture(DbContext context)
    {
        var now = _clock.UtcNow;
        var who = _currentUser.IsAuthenticated ? _currentUser.UserName : "system";
        var captured = new List<CapturedChange>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditEntry)
            {
                continue; // never audit the audit table itself
            }

            if (entry.Entity is AuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAtUtc = now;
                    auditable.CreatedBy = who;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.ModifiedAtUtc = now;
                    auditable.ModifiedBy = who;
                }
            }

            if (entry.Entity is Entity domainEntity &&
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                captured.Add(new CapturedChange(
                    domainEntity,
                    entry.State,
                    Serialize(entry, original: true),
                    Serialize(entry, original: false)));
            }
        }

        return captured;
    }

    /// <summary>
    /// Materialises AuditEntry rows from captured changes. Call this AFTER the
    /// business save so inserted entities have their DB-assigned Id.
    /// </summary>
    public void WriteAuditEntries(DbContext context, IReadOnlyList<CapturedChange> captured)
    {
        if (captured.Count == 0)
        {
            return;
        }

        var now = _clock.UtcNow;
        var actor = _currentUser.UserId;
        var set = context.Set<AuditEntry>();

        foreach (var change in captured)
        {
            var typeName = change.Entity.GetType().Name;
            var action = change.State switch
            {
                EntityState.Added => "Created",
                EntityState.Modified => "Updated",
                _ => "Deleted"
            };

            set.Add(new AuditEntry(
                actor,
                $"{typeName}.{action}",
                typeName,
                ResolveEntityId(context, change.Entity), // real PK, even when it isn't Entity.Id
                change.OldValues,
                change.NewValues,
                _correlation.CorrelationId,
                now));
        }
    }

    /// <summary>The audited entity id. Most entities key on <see cref="Entity.Id"/>, but
    /// some (e.g. DeviceSyncState) key on a different column and ignore Id, leaving it 0.
    /// Read the real primary-key value(s) from EF metadata so the audit trail records the
    /// actual identity rather than "0". Runs after the business save, so inserted keys exist.</summary>
    private static string ResolveEntityId(DbContext context, Entity entity)
    {
        var entry = context.Entry(entity);
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return entity.Id.ToString();
        }

        var parts = key.Properties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? string.Empty);
        return string.Join(":", parts);
    }

    private static string? Serialize(EntityEntry entry, bool original)
    {
        if (original && entry.State == EntityState.Added)
        {
            return null;
        }

        if (!original && entry.State == EntityState.Deleted)
        {
            return null;
        }

        var values = new Dictionary<string, object?>();
        foreach (var prop in entry.Properties)
        {
            // Never write secrets/credential material to audit. (FR-AUD-004.)
            if (prop.Metadata.Name is "PasswordHash" or "TokenHash" or "RowVersion")
            {
                continue;
            }

            values[prop.Metadata.Name] = original ? prop.OriginalValue : prop.CurrentValue;
        }

        return values.Count == 0 ? null : JsonSerializer.Serialize(values, JsonOptions);
    }

    /// <summary>A change captured before save, awaiting its entity Id.</summary>
    public sealed record CapturedChange(
        Entity Entity,
        EntityState State,
        string? OldValues,
        string? NewValues);
}
