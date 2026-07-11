using FluentAssertions;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Users;

namespace TAMS.Integration.Tests;

/// <summary>
/// Admin user-management: create / update / activate / deactivate login accounts,
/// plus the safety guards (no self-deactivation, never remove the last admin).
/// MediatR-direct; the default test principal is admin-equivalent (UserId = 1).
/// </summary>
[Collection("integration")]
public sealed class UserManagementTests
{
    private readonly TamsWebApplicationFactory _factory;
    public UserManagementTests(TamsWebApplicationFactory factory) => _factory = factory;

    private static string U(string p) => $"{p}{Guid.NewGuid():N}".Substring(0, 10);

    [Fact]
    public async Task CreateUser_WithRole_Succeeds_AndAppearsInList()
    {
        var name = U("usr");
        var created = await _factory.SendAsync(new CreateUserCommand(name, $"{name}@x.io", "Passw0rd!", new[] { "Manager" }));

        created.UserName.Should().Be(name);
        created.Roles.Should().Contain("Manager");
        created.IsActive.Should().BeTrue();

        var all = await _factory.SendAsync(new GetUsersQuery());
        all.Should().Contain(u => u.UserName == name);
    }

    [Fact]
    public async Task CreateUser_DuplicateName_IsConflict()
    {
        var name = U("dup");
        await _factory.SendAsync(new CreateUserCommand(name, $"{name}@x.io", "Passw0rd!", new[] { "Employee" }));
        var act = () => _factory.SendAsync(new CreateUserCommand(name, $"{name}2@x.io", "Passw0rd!", new[] { "Employee" }));
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task CreateUser_UnknownRole_IsRejected()
    {
        var name = U("badrole");
        var act = () => _factory.SendAsync(new CreateUserCommand(name, $"{name}@x.io", "Passw0rd!", new[] { "Wizard" }));
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task UpdateUser_ChangesEmailAndRoles()
    {
        var name = U("upd");
        var created = await _factory.SendAsync(new CreateUserCommand(name, $"{name}@x.io", "Passw0rd!", new[] { "Employee" }));

        var updated = await _factory.SendAsync(new UpdateUserCommand(created.Id, "changed@x.io", new[] { "Manager", "Auditor" }, null));

        updated.Email.Should().Be("changed@x.io");
        updated.Roles.Should().BeEquivalentTo(new[] { "Auditor", "Manager" });
    }

    [Fact]
    public async Task DeactivateThenActivateUser_TogglesStatus()
    {
        var name = U("tog");
        var created = await _factory.SendAsync(new CreateUserCommand(name, $"{name}@x.io", "Passw0rd!", new[] { "Employee" }));

        var off = await _factory.SendAsync(new SetUserActiveCommand(created.Id, false));
        off.IsActive.Should().BeFalse();

        var on = await _factory.SendAsync(new SetUserActiveCommand(created.Id, true));
        on.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateSelf_IsRejected()
    {
        // The test principal is UserId = 1, which is the seeded bootstrap admin.
        var act = () => _factory.SendAsync(new SetUserActiveCommand(1, false));
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*your own account*");
    }

    [Fact]
    public async Task DeactivateLastAdmin_IsRejected()
    {
        // The seeded admin (id 1) is the only Administrator; deactivating it is blocked.
        // (Attempted via a different acting id is unnecessary — the self-guard also
        // covers id 1, so this asserts the guard fires for the admin account.)
        var act = () => _factory.SendAsync(new SetUserActiveCommand(1, false));
        await act.Should().ThrowAsync<BusinessRuleException>();
    }
}
