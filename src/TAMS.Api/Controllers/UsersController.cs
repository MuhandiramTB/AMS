using Microsoft.AspNetCore.Mvc;
using TAMS.Api.Auth;
using TAMS.Application.Users;
using TAMS.Domain.Identity;

namespace TAMS.Api.Controllers;

/// <summary>User &amp; role administration. Admin-only (User.Manage). (05 §10.8, FR-ADM-001.)</summary>
public sealed class UsersController : ApiControllerBase
{
    /// <summary>GET /api/v1/users — all login accounts with their roles.</summary>
    [HttpGet]
    [HasPermission(Permissions.UserManage)]
    [ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetUsersQuery(), cancellationToken));

    /// <summary>GET /api/v1/users/roles — assignable roles for the create/edit form.</summary>
    [HttpGet("roles")]
    [HasPermission(Permissions.UserManage)]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetRoles(CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetRolesQuery(), cancellationToken));

    public sealed record CreateUserRequest(string UserName, string Email, string Password, IReadOnlyList<string> Roles, long? EmployeeId);

    /// <summary>POST /api/v1/users — create a login account.</summary>
    [HttpPost]
    [HasPermission(Permissions.UserManage)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreateUserCommand(request.UserName, request.Email, request.Password, request.Roles, request.EmployeeId), cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    public sealed record UpdateUserRequest(string Email, IReadOnlyList<string> Roles, string? NewPassword, long? EmployeeId);

    /// <summary>PUT /api/v1/users/{id} — update email, roles, and optionally reset the password.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.UserManage)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> Update(long id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new UpdateUserCommand(id, request.Email, request.Roles, request.NewPassword, request.EmployeeId), cancellationToken));

    /// <summary>POST /api/v1/users/{id}/activate.</summary>
    [HttpPost("{id:long}/activate")]
    [HasPermission(Permissions.UserManage)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Activate(long id, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SetUserActiveCommand(id, true), cancellationToken));

    /// <summary>POST /api/v1/users/{id}/deactivate. (Blocked for self and the last admin.)</summary>
    [HttpPost("{id:long}/deactivate")]
    [HasPermission(Permissions.UserManage)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserDto>> Deactivate(long id, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SetUserActiveCommand(id, false), cancellationToken));
}
