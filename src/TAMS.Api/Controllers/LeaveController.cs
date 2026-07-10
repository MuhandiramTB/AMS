using Microsoft.AspNetCore.Mvc;
using TAMS.Api.Auth;
using TAMS.Application.Common.Models;
using TAMS.Application.Common.Ports;
using TAMS.Application.Leave;
using TAMS.Domain.Identity;
using TAMS.Domain.Leave;

namespace TAMS.Api.Controllers;

/// <summary>Leave management endpoints. (05 §10.6, FR-LV-*.)</summary>
[Route("api/v1/leave")]
public sealed class LeaveController : ApiControllerBase
{
    public sealed record CreateTypeRequest(string Code, string Name);
    public sealed record SetBalanceRequest(long EmployeeId, long LeaveTypeId, short Year, decimal EntitledDays);
    public sealed record RequestLeaveRequest(long EmployeeId, long LeaveTypeId, DateOnly StartDate, DateOnly EndDate, string? Reason);
    public sealed record ApproveRequest(bool AllowOverride);

    // --- Types ---
    [HttpGet("types")]
    [HasPermission(Permissions.LeaveRead)]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveTypeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LeaveTypeDto>>> GetTypes(CancellationToken ct)
        => Ok(await Mediator.Send(new GetLeaveTypesQuery(), ct));

    [HttpPost("types")]
    [HasPermission(Permissions.LeaveManage)]
    [ProducesResponseType(typeof(LeaveTypeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LeaveTypeDto>> CreateType([FromBody] CreateTypeRequest req, CancellationToken ct)
    {
        var result = await Mediator.Send(new CreateLeaveTypeCommand(req.Code, req.Name), ct);
        return CreatedAtAction(nameof(GetTypes), null, result);
    }

    // --- Balances ---
    [HttpGet("balances")]
    [HasPermission(Permissions.LeaveRead)]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveBalanceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LeaveBalanceDto>>> GetBalances(
        [FromQuery] long employeeId, [FromQuery] short year, CancellationToken ct)
        => Ok(await Mediator.Send(new GetLeaveBalancesQuery(employeeId, year), ct));

    [HttpPut("balances")]
    [HasPermission(Permissions.LeaveManage)]
    [ProducesResponseType(typeof(LeaveBalanceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeaveBalanceDto>> SetBalance([FromBody] SetBalanceRequest req, CancellationToken ct)
        => Ok(await Mediator.Send(new SetLeaveBalanceCommand(req.EmployeeId, req.LeaveTypeId, req.Year, req.EntitledDays), ct));

    // --- Requests ---
    [HttpGet("requests")]
    [HasPermission(Permissions.LeaveRead)]
    [ProducesResponseType(typeof(PagedResult<LeaveRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LeaveRequestDto>>> GetRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? employeeId = null,
        [FromQuery] LeaveStatus? status = null,
        CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetLeaveRequestsQuery(page, pageSize, employeeId, status), ct));

    [HttpPost("requests")]
    [HasPermission(Permissions.LeaveRequest)]
    [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LeaveRequestDto>> SubmitRequest([FromBody] RequestLeaveRequest req, CancellationToken ct)
    {
        var result = await Mediator.Send(
            new RequestLeaveCommand(req.EmployeeId, req.LeaveTypeId, req.StartDate, req.EndDate, req.Reason), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Approve a request. Over-balance is blocked unless override. (BRULE-07.)</summary>
    [HttpPost("requests/{id:long}/approve")]
    [HasPermission(Permissions.LeaveApprove)]
    [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveRequestDto>> Approve(
        long id, [FromBody] ApproveRequest req, [FromServices] ICurrentUser currentUser, CancellationToken ct)
        => Ok(await Mediator.Send(new ApproveLeaveCommand(id, currentUser.UserId ?? 0, req.AllowOverride), ct));

    [HttpPost("requests/{id:long}/reject")]
    [HasPermission(Permissions.LeaveApprove)]
    [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeaveRequestDto>> Reject(
        long id, [FromServices] ICurrentUser currentUser, CancellationToken ct)
        => Ok(await Mediator.Send(new RejectLeaveCommand(id, currentUser.UserId ?? 0), ct));

    [HttpPost("requests/{id:long}/cancel")]
    [HasPermission(Permissions.LeaveRequest)]
    [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeaveRequestDto>> Cancel(long id, CancellationToken ct)
        => Ok(await Mediator.Send(new CancelLeaveCommand(id), ct));
}
