namespace TAMS.Domain.Common;

/// <summary>
/// Base for mutable entities carrying the standard audit columns and a
/// concurrency token. (04_DATABASE_DESIGN.md §3.1, DP-09.)
/// Immutable fact tables (e.g. PunchTransaction, AuditEntry) do NOT derive
/// from this — they are insert-only with no Modified*/RowVersion.
/// </summary>
public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    /// <summary>Optimistic-concurrency token (SQL ROWVERSION).</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
