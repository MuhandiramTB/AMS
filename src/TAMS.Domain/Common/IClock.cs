namespace TAMS.Domain.Common;

/// <summary>
/// Abstraction over the system clock so time-dependent logic is deterministic
/// and testable. Never call DateTime.UtcNow directly in domain/application
/// logic. (07_CODING_STANDARDS.md §10, 03 §7.)
/// </summary>
public interface IClock
{
    /// <summary>Current UTC time. All storage/logic uses UTC. (04 DP-07.)</summary>
    DateTime UtcNow { get; }
}
