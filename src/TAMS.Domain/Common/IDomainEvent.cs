namespace TAMS.Domain.Common;

/// <summary>
/// Marker for domain events. Raised by aggregates and dispatched after a
/// successful commit (used for audit and side effects). (03 §8, ADR-007.)
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
