namespace TAMS.Application.Common.Ports;

/// <summary>
/// Transactional boundary for a use case. One command == one unit of work.
/// Implemented by Infrastructure over EF Core. (03 §9, 07 §4.2.)
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
