namespace TAMS.Application.Common.Ports;

/// <summary>
/// Ambient accessor for the authenticated principal, resolved from the JWT by
/// the Api layer. Used for audit attribution and data-scope authorization.
/// (06 §5, FR-AUD-001.)
/// </summary>
public interface ICurrentUser
{
    long? UserId { get; }

    string UserName { get; }

    bool IsAuthenticated { get; }

    IReadOnlyCollection<string> Permissions { get; }

    /// <summary>The employee this user is linked to, if any (for own-record scoping).</summary>
    long? EmployeeId { get; }

    bool HasPermission(string permission);
}
