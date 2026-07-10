namespace TAMS.Domain.Common;

/// <summary>
/// Thrown when a domain invariant is violated. These represent programmer/state
/// errors that should not normally occur if the application layer validated input;
/// expected business-rule rejections are surfaced via validation, not exceptions.
/// (07_CODING_STANDARDS.md §5.1.)
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
