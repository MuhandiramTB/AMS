using FluentAssertions;
using TAMS.Domain.Identity;

namespace TAMS.Domain.Tests;

public sealed class UserTests
{
    private static readonly DateTime Now = new(2026, 7, 10, 8, 0, 0, DateTimeKind.Utc);

    private static User NewUser() => new("nadia", "nadia@corp.com", "hashed");

    [Fact]
    public void RegisterFailedLogin_BelowThreshold_DoesNotLock()
    {
        var user = NewUser();

        for (var i = 0; i < User.MaxFailedAttempts - 1; i++)
        {
            user.RegisterFailedLogin(Now, TimeSpan.FromMinutes(15));
        }

        user.IsLockedOut(Now).Should().BeFalse();
    }

    [Fact]
    public void RegisterFailedLogin_AtThreshold_LocksAccount()
    {
        var user = NewUser();

        for (var i = 0; i < User.MaxFailedAttempts; i++)
        {
            user.RegisterFailedLogin(Now, TimeSpan.FromMinutes(15));
        }

        user.IsLockedOut(Now).Should().BeTrue();
        user.IsLockedOut(Now.AddMinutes(16)).Should().BeFalse(); // lockout expires
    }

    [Fact]
    public void RegisterSuccessfulLogin_ResetsFailureState()
    {
        var user = NewUser();
        user.RegisterFailedLogin(Now, TimeSpan.FromMinutes(15));

        user.RegisterSuccessfulLogin(Now);

        user.FailedLoginCount.Should().Be(0);
        user.LastLoginUtc.Should().Be(Now);
        user.IsLockedOut(Now).Should().BeFalse();
    }

    [Fact]
    public void PermissionCodes_AggregatesDistinctAcrossRoles()
    {
        var user = NewUser();
        var role = new Role("HROfficer");
        role.GrantPermission(new Permission(Permissions.EmployeeRead));
        role.GrantPermission(new Permission(Permissions.EmployeeWrite));
        role.GrantPermission(new Permission(Permissions.EmployeeRead)); // duplicate ignored
        user.AssignRole(role);

        user.PermissionCodes.Should().BeEquivalentTo(
            new[] { Permissions.EmployeeRead, Permissions.EmployeeWrite });
    }
}
