using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Identity;

namespace TAMS.Application.Common.Security;

/// <summary>
/// Server-derived data scope for read queries (06 §5, OWASP A01). Callers with an
/// unrestricted read permission see all rows; everyone else is confined to their
/// own linked employee. The scope is derived from the authenticated principal —
/// never from client-supplied ids — and any requested filter is intersected with
/// it, so a user cannot widen their own scope.
///
/// This is the P2 seam. When Manager/team features land (later phases), the
/// "team" tier is added here (department-set from the principal) without touching
/// call sites.
/// </summary>
public sealed class DataScope
{
    private DataScope(bool isUnrestricted, long? employeeId)
    {
        IsUnrestricted = isUnrestricted;
        EmployeeId = employeeId;
    }

    public bool IsUnrestricted { get; }

    /// <summary>The only employee id a restricted caller may see (their own).</summary>
    public long? EmployeeId { get; }

    /// <summary>
    /// Builds the scope from the current user. <paramref name="unrestrictedPermission"/>
    /// is the capability that grants all-rows access for this resource.
    /// </summary>
    public static DataScope For(ICurrentUser user, string unrestrictedPermission)
    {
        if (user.HasPermission(unrestrictedPermission))
        {
            return new DataScope(isUnrestricted: true, employeeId: null);
        }

        return new DataScope(isUnrestricted: false, employeeId: user.EmployeeId);
    }

    /// <summary>
    /// Resolves the effective employee-id filter: unrestricted callers keep the
    /// requested filter; restricted callers are pinned to their own employee id
    /// (and are forbidden from requesting someone else's). Returns the id to
    /// filter by, or null for "no employee filter" (unrestricted).
    /// </summary>
    public long? ResolveEmployeeFilter(long? requestedEmployeeId)
    {
        if (IsUnrestricted)
        {
            return requestedEmployeeId;
        }

        if (EmployeeId is null)
        {
            // Restricted caller with no linked employee can see nothing of others.
            throw new ForbiddenException("Your account is not linked to an employee record.");
        }

        if (requestedEmployeeId is not null && requestedEmployeeId != EmployeeId)
        {
            throw new ForbiddenException("You may only access your own records.");
        }

        return EmployeeId;
    }
}
