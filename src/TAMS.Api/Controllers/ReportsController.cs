using Microsoft.AspNetCore.Mvc;
using TAMS.Api.Auth;
using TAMS.Application.Common.Models;
using TAMS.Application.Reporting;
using TAMS.Domain.Identity;

namespace TAMS.Api.Controllers;

/// <summary>Reporting, dashboards and exports. (05 §10.7, FR-RPT-*.)</summary>
[Route("api/v1")]
public sealed class ReportsController : ApiControllerBase
{
    /// <summary>GET /dashboards/attendance-summary — near-real-time snapshot. (FR-RPT-001.)</summary>
    [HttpGet("dashboards/attendance-summary")]
    [HasPermission(Permissions.ReportRead)]
    [ProducesResponseType(typeof(AttendanceSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AttendanceSummaryDto>> AttendanceSummary(
        [FromQuery] DateOnly? workDate = null,
        [FromQuery] long? departmentId = null,
        CancellationToken ct = default)
    {
        var date = workDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(await Mediator.Send(new GetAttendanceSummaryQuery(date, departmentId), ct));
    }

    /// <summary>GET /reports/daily-attendance — paged, filterable, role-scoped. (FR-RPT-002/003/006.)</summary>
    [HttpGet("reports/daily-attendance")]
    [HasPermission(Permissions.ReportRead)]
    [ProducesResponseType(typeof(PagedResult<DailyAttendanceRowDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<DailyAttendanceRowDto>>> DailyAttendance(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] long? employeeId = null,
        [FromQuery] long? departmentId = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
        => Ok(await Mediator.Send(
            new GetDailyAttendanceQuery(page, pageSize, fromDate, toDate, employeeId, departmentId, status), ct));

    /// <summary>GET /reports/exceptions — open attendance exceptions. (FR-RPT-002.)</summary>
    [HttpGet("reports/exceptions")]
    [HasPermission(Permissions.ReportRead)]
    [ProducesResponseType(typeof(IReadOnlyList<ExceptionRowDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExceptionRowDto>>> Exceptions(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] long? departmentId = null,
        CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetExceptionsReportQuery(fromDate, toDate, departmentId), ct));

    /// <summary>GET /reports/daily-attendance/export — CSV download, audited. (FR-RPT-004/007.)</summary>
    [HttpGet("reports/daily-attendance/export")]
    [HasPermission(Permissions.ReportExport)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportDailyAttendance(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] long? employeeId = null,
        [FromQuery] long? departmentId = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var file = await Mediator.Send(
            new ExportDailyAttendanceCommand(fromDate, toDate, employeeId, departmentId, status), ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>GET /reports/payroll-export — worked-hours/OT feed, audited. (FR-RPT-005/007.)</summary>
    [HttpGet("reports/payroll-export")]
    [HasPermission(Permissions.ReportExport)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportPayroll(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] long? departmentId = null,
        CancellationToken ct = default)
    {
        var file = await Mediator.Send(new ExportPayrollCommand(fromDate, toDate, departmentId), ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
