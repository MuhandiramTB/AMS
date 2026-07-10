using TAMS.Domain.Common;

namespace TAMS.Domain.Identity;

/// <summary>
/// A single grantable capability (e.g. "Employee.Write"). Roles bundle
/// permissions; authorization policies check permission codes, not role names.
/// (02 §4.1, 06 §5, 04 Identity.Permission.)
/// </summary>
public sealed class Permission : Entity
{
    private Permission()
    {
    }

    public Permission(string code, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Permission code is required.");
        }

        Code = code.Trim();
        Description = description;
    }

    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }
}
