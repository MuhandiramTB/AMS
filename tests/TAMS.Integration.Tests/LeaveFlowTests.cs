using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TAMS.Application.Attendance;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Departments;
using TAMS.Application.Employees;
using TAMS.Application.Leave;
using TAMS.Domain.Attendance;
using TAMS.Infrastructure.Persistence;

namespace TAMS.Integration.Tests;

/// <summary>
/// Leave flow through the real stack: balance enforcement (BRULE-07), the
/// leave↔attendance link (FR-ATT-007 / BRULE-06 — approved leave overrides
/// absence), and cancel-releases-balance. MediatR-direct for stability.
/// </summary>
[Collection("integration")]
public sealed class LeaveFlowTests
{
    private readonly TamsWebApplicationFactory _factory;
    public LeaveFlowTests(TamsWebApplicationFactory factory) => _factory = factory;

    private static string U(string p) => $"{p}{Guid.NewGuid():N}".Substring(0, 12);

    private async Task<(long EmpId, long TypeId)> SetupEmployeeAndTypeAsync()
    {
        var s = U("L");
        var dept = await _factory.SendAsync(new CreateDepartmentCommand($"LD{s}", $"LDept{s}", null));
        var emp = await _factory.SendAsync(new CreateEmployeeCommand($"LE{s}", "Lea", "Ve", null, dept.Id, null));
        var type = await _factory.SendAsync(new CreateLeaveTypeCommand($"AN{s}", "Annual"));
        return (emp.Id, type.Id);
    }

    private async Task<AttendanceStatus> RecordStatusAsync(long empId, DateOnly date)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TamsDbContext>();
        var rec = await db.AttendanceRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.EmployeeId == empId && r.WorkDate == date);
        return rec!.Status;
    }

    [Fact]
    public async Task ApproveBeyondBalance_IsBlocked_UnlessOverride()
    {
        var (empId, typeId) = await SetupEmployeeAndTypeAsync();
        await _factory.SendAsync(new SetLeaveBalanceCommand(empId, typeId, 2026, EntitledDays: 2m));

        // Request 3 days against a 2-day balance.
        var req = await _factory.SendAsync(new RequestLeaveCommand(
            empId, typeId, new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 22), "trip"));

        // Approve without override → blocked (BRULE-07).
        var act = () => _factory.SendAsync(new ApproveLeaveCommand(req.Id, ApproverUserId: 1, AllowOverride: false));
        await act.Should().ThrowAsync<BusinessRuleException>();

        // With override → succeeds.
        var approved = await _factory.SendAsync(new ApproveLeaveCommand(req.Id, 1, AllowOverride: true));
        approved.Status.Should().Be("Applied");
    }

    [Fact]
    public async Task ApprovedLeave_OverridesAbsence_InAttendance()
    {
        // FR-ATT-007 / BRULE-06: a day with approved leave and NO punches must not
        // be flagged absent — it processes cleanly as leave-covered.
        var (empId, typeId) = await SetupEmployeeAndTypeAsync();
        await _factory.SendAsync(new SetLeaveBalanceCommand(empId, typeId, 2026, EntitledDays: 20m));
        var day = new DateOnly(2026, 7, 21);

        // No punches for the day. Process first → should be an Exception (absent).
        await _factory.SendAsync(new ProcessAttendanceCommand(empId, day));
        (await RecordStatusAsync(empId, day)).Should().Be(AttendanceStatus.Exception);

        // Approve leave covering the day → approval reprocesses attendance.
        var req = await _factory.SendAsync(new RequestLeaveCommand(empId, typeId, day, day, "sick"));
        await _factory.SendAsync(new ApproveLeaveCommand(req.Id, 1));

        // The day is now leave-covered → Processed, not an absent Exception.
        (await RecordStatusAsync(empId, day)).Should().Be(AttendanceStatus.Processed);
    }

    [Fact]
    public async Task ApproveThenCancel_ReleasesBalance()
    {
        var (empId, typeId) = await SetupEmployeeAndTypeAsync();
        await _factory.SendAsync(new SetLeaveBalanceCommand(empId, typeId, 2026, EntitledDays: 10m));

        var req = await _factory.SendAsync(new RequestLeaveCommand(
            empId, typeId, new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 22), "trip")); // 3 days
        await _factory.SendAsync(new ApproveLeaveCommand(req.Id, 1));

        var afterApprove = await _factory.SendAsync(new GetLeaveBalancesQuery(empId, 2026));
        afterApprove.Single(b => b.LeaveTypeId == typeId).RemainingDays.Should().Be(7m);

        await _factory.SendAsync(new CancelLeaveCommand(req.Id));

        var afterCancel = await _factory.SendAsync(new GetLeaveBalancesQuery(empId, 2026));
        afterCancel.Single(b => b.LeaveTypeId == typeId).RemainingDays.Should().Be(10m); // released
    }
}
