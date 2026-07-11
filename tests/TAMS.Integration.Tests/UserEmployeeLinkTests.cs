using FluentAssertions;
using TAMS.Application.Attendance;
using TAMS.Application.Departments;
using TAMS.Application.Employees;
using TAMS.Application.Users;
using TAMS.Domain.Identity;

namespace TAMS.Integration.Tests;

/// <summary>
/// Linking a login account to an employee record: the create/update commands set
/// it, and a linked restricted user can then read their OWN attendance instead of
/// being 403'd for "no linked employee". (Fixes the scoped-page failure.)
/// </summary>
[Collection("integration")]
public sealed class UserEmployeeLinkTests
{
    private readonly TamsWebApplicationFactory _factory;
    public UserEmployeeLinkTests(TamsWebApplicationFactory factory) => _factory = factory;

    private static string U(string p) => $"{p}{Guid.NewGuid():N}".Substring(0, 10);

    [Fact]
    public async Task CreateUser_LinkedToEmployee_StoresTheLink()
    {
        var s = U("lk");
        var dept = await _factory.SendAsync(new CreateDepartmentCommand($"LKD{s}", $"Dept{s}", null));
        var emp = await _factory.SendAsync(new CreateEmployeeCommand($"LKE{s}", "Lin", "Ked", null, dept.Id, null));

        var user = await _factory.SendAsync(new CreateUserCommand($"u{s}", $"{s}@x.io", "Passw0rd!", new[] { "Employee" }, emp.Id));

        user.EmployeeId.Should().Be(emp.Id);
    }

    [Fact]
    public async Task CreateUser_LinkedToUnknownEmployee_IsRejected()
    {
        var s = U("bad");
        var act = () => _factory.SendAsync(new CreateUserCommand($"u{s}", $"{s}@x.io", "Passw0rd!", new[] { "Employee" }, 999999));
        await act.Should().ThrowAsync<TAMS.Application.Common.Exceptions.BusinessRuleException>();
    }

    [Fact]
    public async Task UpdateUser_CanLinkAndUnlinkEmployee()
    {
        var s = U("upl");
        var dept = await _factory.SendAsync(new CreateDepartmentCommand($"ULD{s}", $"Dept{s}", null));
        var emp = await _factory.SendAsync(new CreateEmployeeCommand($"ULE{s}", "Up", "Link", null, dept.Id, null));
        var user = await _factory.SendAsync(new CreateUserCommand($"u{s}", $"{s}@x.io", "Passw0rd!", new[] { "Employee" }));
        user.EmployeeId.Should().BeNull();

        var linked = await _factory.SendAsync(new UpdateUserCommand(user.Id, $"{s}@x.io", new[] { "Employee" }, null, emp.Id));
        linked.EmployeeId.Should().Be(emp.Id);

        var unlinked = await _factory.SendAsync(new UpdateUserCommand(user.Id, $"{s}@x.io", new[] { "Employee" }, null, null));
        unlinked.EmployeeId.Should().BeNull();
    }

    [Fact]
    public async Task LinkedRestrictedUser_CanReadOwnAttendance_NotForbidden()
    {
        // Set up an employee with a processed attendance record.
        var s = U("own");
        var dept = await _factory.SendAsync(new CreateDepartmentCommand($"OWD{s}", $"Dept{s}", null));
        var emp = await _factory.SendAsync(new CreateEmployeeCommand($"OWE{s}", "Own", "Rec", null, dept.Id, null));
        var day = new DateOnly(2026, 4, 1);
        await _factory.SendAsync(new ProcessAttendanceCommand(emp.Id, day));

        // Impersonate a restricted Employee LINKED to that employee: reading their own
        // records must succeed (no ForbiddenException).
        var principal = new TestPrincipal(emp.Id, new[] { Permissions.AttendanceRead });
        await TestPrincipal.RunAs(principal, async () =>
        {
            var result = await _factory.SendAsync(new GetAttendanceRecordsQuery(1, 20, null, day, day));
            result.Items.Should().OnlyContain(r => r.EmployeeId == emp.Id);
        });
    }
}
