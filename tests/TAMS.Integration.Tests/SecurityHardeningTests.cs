using FluentAssertions;
using TAMS.Application.Attendance;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Departments;
using TAMS.Application.Employees;
using TAMS.Application.Leave;
using TAMS.Application.Reporting;
using TAMS.Domain.Identity;

namespace TAMS.Integration.Tests;

/// <summary>
/// P6 security-hardening regression tests. Lock in the data-scope / access-control
/// fixes so a future change cannot silently reopen the IDOR / broken-access holes.
/// A restricted Employee principal is impersonated via TestPrincipal.RunAs to
/// exercise the DataScope path that the default admin-equivalent principal bypasses.
/// </summary>
[Collection("integration")]
public sealed class SecurityHardeningTests
{
    private readonly TamsWebApplicationFactory _factory;
    public SecurityHardeningTests(TamsWebApplicationFactory factory) => _factory = factory;

    private static string U(string p) => $"{p}{Guid.NewGuid():N}".Substring(0, 12);

    private async Task<long> CreateEmployeeAsync()
    {
        var s = U("S");
        var dept = await _factory.SendAsync(new CreateDepartmentCommand($"SD{s}", $"SDept{s}", null));
        var emp = await _factory.SendAsync(new CreateEmployeeCommand($"SE{s}", "Sec", "Ure", null, dept.Id, null));
        return emp.Id;
    }

    // A plain Employee: no *.ReadAll, no LeaveManage — confined to their own records.
    private static TestPrincipal EmployeePrincipal(long employeeId) => new(
        employeeId,
        new[] { Permissions.AttendanceRead, Permissions.LeaveRead, Permissions.LeaveRequest });

    [Fact]
    public async Task AttendanceById_RestrictedUser_CannotReadAnotherEmployeesRecord()
    {
        var mine = await CreateEmployeeAsync();
        var theirs = await CreateEmployeeAsync();
        var day = new DateOnly(2026, 5, 4);
        await _factory.SendAsync(new ProcessAttendanceCommand(theirs, day)); // creates their record

        // Find their record id (as admin).
        var theirRecords = await _factory.SendAsync(new GetAttendanceRecordsQuery(1, 50, theirs, day, day));
        var theirRecordId = theirRecords.Items.Single().Id;

        // As the OTHER employee, fetching that id must look like not-found (no IDOR).
        await TestPrincipal.RunAs(EmployeePrincipal(mine), async () =>
        {
            var act = () => _factory.SendAsync(new GetAttendanceRecordByIdQuery(theirRecordId));
            await act.Should().ThrowAsync<NotFoundException>();
        });
    }

    [Fact]
    public async Task AttendanceById_RestrictedUser_CanReadOwnRecord()
    {
        var mine = await CreateEmployeeAsync();
        var day = new DateOnly(2026, 5, 5);
        await _factory.SendAsync(new ProcessAttendanceCommand(mine, day));
        var mineRecords = await _factory.SendAsync(new GetAttendanceRecordsQuery(1, 50, mine, day, day));
        var myRecordId = mineRecords.Items.Single().Id;

        await TestPrincipal.RunAs(EmployeePrincipal(mine), async () =>
        {
            var record = await _factory.SendAsync(new GetAttendanceRecordByIdQuery(myRecordId));
            record.EmployeeId.Should().Be(mine);
        });
    }

    [Fact]
    public async Task LeaveRequests_RestrictedUser_CannotQueryAnotherEmployee()
    {
        var mine = await CreateEmployeeAsync();
        var theirs = await CreateEmployeeAsync();

        await TestPrincipal.RunAs(EmployeePrincipal(mine), async () =>
        {
            var act = () => _factory.SendAsync(new GetLeaveRequestsQuery(1, 20, theirs, null));
            await act.Should().ThrowAsync<ForbiddenException>();
        });
    }

    [Fact]
    public async Task LeaveBalances_RestrictedUser_CannotQueryAnotherEmployee()
    {
        var mine = await CreateEmployeeAsync();
        var theirs = await CreateEmployeeAsync();

        await TestPrincipal.RunAs(EmployeePrincipal(mine), async () =>
        {
            var act = () => _factory.SendAsync(new GetLeaveBalancesQuery(theirs, 2026));
            await act.Should().ThrowAsync<ForbiddenException>();
        });
    }

    [Fact]
    public async Task RequestLeave_RestrictedUser_CannotSubmitOnBehalfOfAnother()
    {
        var mine = await CreateEmployeeAsync();
        var theirs = await CreateEmployeeAsync();
        var type = await _factory.SendAsync(new CreateLeaveTypeCommand(U("T"), "Annual"));

        await TestPrincipal.RunAs(EmployeePrincipal(mine), async () =>
        {
            var act = () => _factory.SendAsync(new RequestLeaveCommand(
                theirs, type.Id, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 2), "forged"));
            await act.Should().ThrowAsync<ForbiddenException>();
        });
    }

    [Fact]
    public async Task ExportDailyAttendance_WithoutDateRange_IsRejected()
    {
        // Unbounded export (no From/To) must be rejected by validation, not stream
        // the whole table. (Perf/DoS hardening.)
        var act = () => _factory.SendAsync(
            new ExportDailyAttendanceCommand(null, null, null, null, null));
        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }
}
