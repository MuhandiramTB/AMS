using Microsoft.AspNetCore.Mvc;
using TAMS.Api.Auth;
using TAMS.Application.Common.Models;
using TAMS.Application.Employees;
using TAMS.Domain.Identity;

namespace TAMS.Api.Controllers;

/// <summary>Employee endpoints. (05 §10.1, FR-EMP-*.)</summary>
public sealed class EmployeesController : ApiControllerBase
{
    public sealed record CreateEmployeeRequest(
        string EmployeeNo,
        string FirstName,
        string LastName,
        string? Email,
        long PrimaryDepartmentId,
        DateOnly? HireDate);

    /// <summary>GET /api/v1/employees — paged, filterable list.</summary>
    [HttpGet]
    [HasPermission(Permissions.EmployeeRead)]
    [ProducesResponseType(typeof(PagedResult<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EmployeeDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? departmentId = null,
        [FromQuery] string? q = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(
            new GetEmployeesQuery(page, pageSize, departmentId, q), cancellationToken);
        return Ok(result);
    }

    /// <summary>GET /api/v1/employees/{id}.</summary>
    [HttpGet("{id:long}")]
    [HasPermission(Permissions.EmployeeRead)]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetEmployeeByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>POST /api/v1/employees.</summary>
    [HttpPost]
    [HasPermission(Permissions.EmployeeWrite)]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeDto>> Create(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreateEmployeeCommand(
            request.EmployeeNo,
            request.FirstName,
            request.LastName,
            request.Email,
            request.PrimaryDepartmentId,
            request.HireDate), cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
