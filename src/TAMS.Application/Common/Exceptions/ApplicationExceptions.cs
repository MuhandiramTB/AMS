namespace TAMS.Application.Common.Exceptions;

/// <summary>
/// Base for expected, mappable application failures. The global exception
/// handler translates these to RFC 9457 problem details with the right status.
/// (05 §5/§6, 07 §5.1.)
/// </summary>
public abstract class ApplicationException : Exception
{
    protected ApplicationException(string message) : base(message)
    {
    }
}

/// <summary>Requested resource does not exist → 404.</summary>
public sealed class NotFoundException : ApplicationException
{
    public NotFoundException(string entity, object key)
        : base($"{entity} '{key}' was not found.")
    {
    }
}

/// <summary>State/uniqueness conflict (e.g. duplicate business key) → 409.</summary>
public sealed class ConflictException : ApplicationException
{
    public ConflictException(string message) : base(message)
    {
    }
}

/// <summary>Business-rule rejection (semantically invalid) → 422.</summary>
public sealed class BusinessRuleException : ApplicationException
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}

/// <summary>Authenticated but not permitted / out of scope → 403.</summary>
public sealed class ForbiddenException : ApplicationException
{
    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base(message)
    {
    }
}

/// <summary>
/// Authentication failed → 401. Message is intentionally generic to avoid user
/// enumeration. (06 §4, FR-AUTH-001.)
/// </summary>
public sealed class AuthenticationException : ApplicationException
{
    public AuthenticationException()
        : base("Invalid credentials.")
    {
    }
}

/// <summary>Account is locked due to repeated failed logins → 423. (FR-AUTH-005.)</summary>
public sealed class AccountLockedException : ApplicationException
{
    public AccountLockedException()
        : base("Account is temporarily locked. Please try again later.")
    {
    }
}
