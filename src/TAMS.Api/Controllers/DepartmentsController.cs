using Microsoft.AspNetCore.Mvc;
using TAMS.Api.Auth;
using TAMS.Application.Departments;
using TAMS.Domain.Identity;

namespace TAMS.Api.Controllers;

/// <summary>Department endpoints. (05 §10.2, FR-DEP-*.)</summary>
public sealed class DepartmentsController : ApiControllerBase
{
    public sealed record CreateDepartmentRequest(string Code, string Name, long? ParentDepartmentId);

    /// <summary>GET /api/v1/departments — list, optionally filtered by parent.</summary>
    [HttpGet]
    [HasPermission(Permissions.DepartmentRead)]
    [ProducesResponseType(typeof(IReadOnlyList<DepartmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetAll(
        [FromQuery] long? parentId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(new GetDepartmentsQuery(parentId), cancellationToken);
        return Ok(result);
    }

    /// <summary>POST /api/v1/departments.</summary>
    [HttpPost]
    [HasPermission(Permissions.DepartmentWrite)]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DepartmentDto>> Create(
        [FromBody] CreateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new CreateDepartmentCommand(request.Code, request.Name, request.ParentDepartmentId),
            cancellationToken);

        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    public sealed record UpdateDepartmentRequest(string Name, long? ParentDepartmentId);

    /// <summary>PUT /api/v1/departments/{id} — rename / re-parent.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.DepartmentWrite)]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentDto>> Update(long id, [FromBody] UpdateDepartmentRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new UpdateDepartmentCommand(id, request.Name, request.ParentDepartmentId), cancellationToken));

    /// <summary>POST /api/v1/departments/{id}/activate.</summary>
    [HttpPost("{id:long}/activate")]
    [HasPermission(Permissions.DepartmentWrite)]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DepartmentDto>> Activate(long id, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SetDepartmentActiveCommand(id, true), cancellationToken));

    /// <summary>POST /api/v1/departments/{id}/deactivate. (Blocked if it has active employees.)</summary>
    [HttpPost("{id:long}/deactivate")]
    [HasPermission(Permissions.DepartmentWrite)]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DepartmentDto>> Deactivate(long id, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SetDepartmentActiveCommand(id, false), cancellationToken));
}
