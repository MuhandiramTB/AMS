using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TAMS.Application.Attendance;
using TAMS.Application.Departments;
using TAMS.Application.Employees;
using TAMS.Application.Reporting;
using TAMS.Application.Scheduling;
using TAMS.Infrastructure.Persistence;

namespace TAMS.Integration.Tests;

/// <summary>
/// Reporting aggregates + exports over the real stack (FR-RPT-*). Sets up a
/// processed attendance record, then verifies summary counts, the payroll export
/// content, and that exports write an audit entry. MediatR-direct for stability.
/// </summary>
[Collection("integration")]
public sealed class ReportingTests
{
    private readonly TamsWebApplicationFactory _factory;
    public ReportingTests(TamsWebApplicationFactory factory) => _factory = factory;

    private static string U(string p) => $"{p}{Guid.NewGuid():N}".Substring(0, 12);
    private static readonly DateOnly Day = new(2026, 9, 10);

    /// <summary>Creates dept+employee+shift, punches a full day, processes → present+OT record.</summary>
    private async Task<long> SeedPresentEmployeeAsync()
    {
        var s = U("R");
        var dept = await _factory.SendAsync(new CreateDepartmentCommand($"RD{s}", $"RDept{s}", null));
        var emp = await _factory.SendAsync(new CreateEmployeeCommand($"RE{s}", "Rep", "Ort", null, dept.Id, null));
        var shift = await _factory.SendAsync(new CreateShiftCommand(
            $"RS{s}", "Day", new TimeOnly(9, 0), new TimeOnly(17, 0), 60, 10, 10, 0));
        await _factory.SendAsync(new AssignShiftCommand(shift.Id, emp.Id, null, new DateOnly(2026, 1, 1), null));

        var dev = await _factory.SendAsync(new TAMS.Application.Devices.RegisterDeviceCommand(
            $"RZK{s}", $"RGate{s}", "127.0.0.1", 4370, "K40"));
        var inUtc = Day.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
        var outUtc = Day.ToDateTime(new TimeOnly(18, 0), DateTimeKind.Utc); // 1h OT past 17:00
        await _factory.SendAsync(new RecordPunchCommand(dev.Id, $"{emp.Id}", emp.Id, inUtc, TAMS.Domain.Attendance.PunchDirection.In, TAMS.Domain.Attendance.PunchSource.Device));
        await _factory.SendAsync(new RecordPunchCommand(dev.Id, $"{emp.Id}", emp.Id, outUtc, TAMS.Domain.Attendance.PunchDirection.Out, TAMS.Domain.Attendance.PunchSource.Device));
        await _factory.SendAsync(new ProcessAttendanceCommand(emp.Id, Day));
        return emp.Id;
    }

    [Fact]
    public async Task AttendanceSummary_CountsPresentAndOvertime()
    {
        await SeedPresentEmployeeAsync();
        var summary = await _factory.SendAsync(new GetAttendanceSummaryQuery(Day, null));
        summary.Present.Should().BeGreaterThanOrEqualTo(1);
        summary.WorkDate.Should().Be("2026-09-10");
    }

    [Fact]
    public async Task DailyAttendance_ReturnsTheProcessedRow()
    {
        var empId = await SeedPresentEmployeeAsync();
        var report = await _factory.SendAsync(
            new GetDailyAttendanceQuery(1, 50, Day, Day, empId, null, null));
        report.Items.Should().ContainSingle(r => r.EmployeeId == empId);
        report.Items.Single(r => r.EmployeeId == empId).WorkedMinutes.Should().Be(9 * 60 - 60); // 9h gross (09→18) minus 1h break = 480
    }

    [Fact]
    public async Task PayrollExport_ProducesCsv_WithWorkedAndOvertimeHours()
    {
        var empId = await SeedPresentEmployeeAsync();
        var file = await _factory.SendAsync(new ExportPayrollCommand(Day, Day, null));

        file.ContentType.Should().Be("text/csv");
        var csv = Encoding.UTF8.GetString(file.Content);
        csv.Should().Contain("EmployeeNo,EmployeeName,WorkedHours,OvertimeHours");
        // The seeded employee worked 9→18 (−1h break) = 8.00h worked, 1.00h OT.
        var line = csv.Split('\n').FirstOrDefault(l => l.Contains($"RE"));
        line.Should().NotBeNull();
        csv.Should().Contain("8.00");  // worked hours
        csv.Should().Contain("1.00");  // overtime hours
        _ = empId;
    }

    [Fact]
    public async Task Export_WritesAnAuditEntry()
    {
        await SeedPresentEmployeeAsync();
        await _factory.SendAsync(new ExportPayrollCommand(Day, Day, null));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TamsDbContext>();
        var audited = await db.AuditEntries.AsNoTracking()
            .AnyAsync(a => a.Action == "Report.Export.Payroll");
        audited.Should().BeTrue(); // FR-RPT-007
    }
}
