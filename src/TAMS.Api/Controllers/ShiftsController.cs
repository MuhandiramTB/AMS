using Microsoft.AspNetCore.Mvc;
using TAMS.Api.Auth;
using TAMS.Application.Scheduling;
using TAMS.Domain.Identity;

namespace TAMS.Api.Controllers;

/// <summary>Shift + assignment endpoints. (05 §10.3, FR-SFT-*.)</summary>
public sealed class ShiftsController : ApiControllerBase
{
    public sealed record CreateShiftRequest(
        string Code, string Name, TimeOnly StartTime, TimeOnly EndTime,
        int BreakMinutes, int GraceInMinutes, int GraceOutMinutes, int OvertimeThresholdMinutes);

    public sealed record AssignShiftRequest(
        long ShiftId, long? EmployeeId, long? DepartmentId, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

    [HttpGet]
    [HasPermission(Permissions.ShiftRead)]
    [ProducesResponseType(typeof(IReadOnlyList<ShiftDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ShiftDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetShiftsQuery(), cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.ShiftWrite)]
    [ProducesResponseType(typeof(ShiftDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShiftDto>> Create(
        [FromBody] CreateShiftRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreateShiftCommand(
            request.Code, request.Name, request.StartTime, request.EndTime,
            request.BreakMinutes, request.GraceInMinutes, request.GraceOutMinutes,
            request.OvertimeThresholdMinutes), cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    [HttpPost("assignments")]
    [HasPermission(Permissions.ShiftWrite)]
    [ProducesResponseType(typeof(ShiftAssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ShiftAssignmentDto>> Assign(
        [FromBody] AssignShiftRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new AssignShiftCommand(
            request.ShiftId, request.EmployeeId, request.DepartmentId,
            request.EffectiveFrom, request.EffectiveTo), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
