namespace TAMS.Domain.Common;

/// <summary>
/// Base class for all persistent entities. Provides a surrogate identity
/// and domain-event collection. (04_DATABASE_DESIGN.md DP-02, 03 §8.)
/// </summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>Surrogate primary key (BIGINT IDENTITY in the DB).</summary>
    public long Id { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
