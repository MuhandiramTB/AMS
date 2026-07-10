using Microsoft.AspNetCore.Mvc;
using TAMS.Api.Auth;
using TAMS.Application.Attendance;
using TAMS.Application.Common.Models;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Attendance;
using TAMS.Domain.Identity;

namespace TAMS.Api.Controllers;

/// <summary>Punch + attendance-record endpoints. (05 §10.5, FR-ATT-*.)</summary>
[Microsoft.AspNetCore.Mvc.Route("api/v1/attendance")]
public sealed class AttendanceController : ApiControllerBase
{
    public sealed record RecordPunchRequest(
        long DeviceId, string DeviceUserId, long? EmployeeId,
        DateTime PunchedAtUtc, PunchDirection Direction);

    public sealed record ProcessRequest(long EmployeeId, DateOnly WorkDate);

    public sealed record CorrectRequest(DateTime? FirstInUtc, DateTime? LastOutUtc, string Reason);

    /// <summary>POST manual punch (device-fed in P3). Idempotent. (FR-ATT-001.)</summary>
    [HttpPost("punches")]
    [HasPermission(Permissions.AttendanceWrite)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordPunch(
        [FromBody] RecordPunchRequest request, CancellationToken cancellationToken)
    {
        var added = await Mediator.Send(new RecordPunchCommand(
            request.DeviceId, request.DeviceUserId, request.EmployeeId,
            request.PunchedAtUtc, request.Direction, PunchSource.ManualEntry), cancellationToken);

        // 201 when newly stored; 200 when the idempotency key was a duplicate.
        return added ? StatusCode(StatusCodes.Status201Created) : Ok(new { duplicate = true });
    }

    /// <summary>Process/recompute an employee's attendance for a date. (FR-ATT-002/009.)</summary>
    [HttpPost("process")]
    [HasPermission(Permissions.AttendanceWrite)]
    [ProducesResponseType(typeof(AttendanceRecordDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AttendanceRecordDto>> Process(
        [FromBody] ProcessRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new ProcessAttendanceCommand(request.EmployeeId, request.WorkDate), cancellationToken));

    [HttpGet("records")]
    [HasPermission(Permissions.AttendanceRead)]
    [ProducesResponseType(typeof(PagedResult<AttendanceRecordDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AttendanceRecordDto>>> GetRecords(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? employeeId = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
        => Ok(await Mediator.Send(
            new GetAttendanceRecordsQuery(page, pageSize, employeeId, fromDate, toDate), cancellationToken));

    [HttpGet("records/{id:long}")]
    [HasPermission(Permissions.AttendanceRead)]
    [ProducesResponseType(typeof(AttendanceRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttendanceRecordDto>> GetRecord(long id, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetAttendanceRecordByIdQuery(id), cancellationToken));

    /// <summary>Correct a record's in/out with a mandatory reason. (FR-ATT-006, BRULE-05.)</summary>
    [HttpPatch("records/{id:long}")]
    [HasPermission(Permissions.AttendanceCorrect)]
    [ProducesResponseType(typeof(AttendanceRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttendanceRecordDto>> Correct(
        long id,
        [FromBody] CorrectRequest request,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? 0;
        var result = await Mediator.Send(new CorrectAttendanceCommand(
            id, actorId, request.FirstInUtc, request.LastOutUtc, request.Reason), cancellationToken);
        return Ok(result);
    }
}
