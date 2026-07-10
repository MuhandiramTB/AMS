using TAMS.Domain.Common;

namespace TAMS.Domain.Identity;

/// <summary>
/// A named bundle of permissions. (02 §4.1, 04 Identity.Role/RolePermission.)
/// </summary>
public sealed class Role : Entity
{
    private readonly List<Permission> _permissions = new();

    private Role()
    {
    }

    public Role(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Role name is required.");
        }

        Name = name.Trim();
        Description = description;
    }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public IReadOnlyCollection<Permission> Permissions => _permissions.AsReadOnly();

    public void GrantPermission(Permission permission)
    {
        if (_permissions.Any(p => p.Code == permission.Code))
        {
            return;
        }

        _permissions.Add(permission);
    }

    public void RevokePermission(string permissionCode)
        => _permissions.RemoveAll(p => p.Code == permissionCode);
}
