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

    [HttpGet("unresolved-punches")]
    [HasPermission(Permissions.DeviceManage)]
    [ProducesResponseType(typeof(PagedResult<UnresolvedPunchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UnresolvedPunchDto>>> GetUnresolvedPunches(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? deviceId = null,
        CancellationToken cancellationToken = default)
        => Ok(await Mediator.Send(
            new GetUnresolvedPunchesQuery(page, pageSize, deviceId), cancellationToken));

    [HttpGet("records/{id:long}")]
    [HasPermission(Permissions.AttendanceRead)]
    [ProducesResponseType(typeof(AttendanceRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttendanceRecordDto>> GetRecord(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetAttendanceRecordByIdQuery(id), cancellationToken);
        SetETag(result.ConcurrencyToken);
        return Ok(result);
    }

    /// <summary>
    /// Correct a record's in/out with a mandatory reason. Requires an If-Match
    /// header carrying the record's ETag for optimistic concurrency.
    /// (FR-ATT-006, BRULE-05, 05 §8.2/§10.5.)
    /// </summary>
    [HttpPatch("records/{id:long}")]
    [HasPermission(Permissions.AttendanceCorrect)]
    [ProducesResponseType(typeof(AttendanceRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult<AttendanceRecordDto>> Correct(
        long id,
        [FromBody] CorrectRequest request,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        // If-Match is mandatory so a correction can't silently clobber a concurrent
        // edit. Absence → 428 Precondition Required. (05 §8.2.)
        var ifMatch = ParseIfMatch();
        if (ifMatch is null)
        {
            return StatusCode(StatusCodes.Status428PreconditionRequired,
                new { detail = "An If-Match header with the record's ETag is required." });
        }

        var actorId = currentUser.UserId ?? 0;
        var result = await Mediator.Send(new CorrectAttendanceCommand(
            id, actorId, request.FirstInUtc, request.LastOutUtc, request.Reason, ifMatch), cancellationToken);
        SetETag(result.ConcurrencyToken);
        return Ok(result);
    }

    private void SetETag(string concurrencyToken)
    {
        if (!string.IsNullOrEmpty(concurrencyToken))
        {
            Response.Headers.ETag = $"\"{concurrencyToken}\"";
        }
    }

    /// <summary>Extracts the (quoted) ETag value from the If-Match header, if present.</summary>
    private string? ParseIfMatch()
    {
        var raw = Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim().Trim('"');
    }
}
