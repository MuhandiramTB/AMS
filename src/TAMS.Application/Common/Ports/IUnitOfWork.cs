namespace TAMS.Application.Common.Ports;

/// <summary>
/// Transactional boundary for a use case. One command == one unit of work.
/// Implemented by Infrastructure over EF Core. (03 §9, 07 §4.2.)
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs <paramref name="action"/> inside a single database transaction so
    /// that a multi-step use case (several SaveChanges + side effects) commits or rolls
    /// back atomically. Compatible with the configured retry execution strategy — the
    /// action may be re-invoked, so keep it idempotent. (07 §4.2.)</summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
