using Microsoft.AspNetCore.Mvc;
using TAMS.Api.Auth;
using TAMS.Application.Devices;
using TAMS.Domain.Identity;

namespace TAMS.Api.Controllers;

/// <summary>Device management + enrollment endpoints. (05 §10.4, FR-ZK-010, FR-EMP-004.)</summary>
public sealed class DevicesController : ApiControllerBase
{
    public sealed record RegisterDeviceRequest(string SerialNo, string Name, string? IpAddress, int? Port, string? Model);
    public sealed record UpdateDeviceRequest(string Name, string? IpAddress, int? Port, string? Model);
    public sealed record EnrollRequest(long EmployeeId, string DeviceUserId);

    [HttpGet]
    [HasPermission(Permissions.DeviceRead)]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DeviceDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetDevicesQuery(), cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.DeviceManage)]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeviceDto>> Register(
        [FromBody] RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new RegisterDeviceCommand(
            request.SerialNo, request.Name, request.IpAddress, request.Port, request.Model), cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    /// <summary>PUT /devices/{id} — edit device details. (FR-ZK-010.)</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.DeviceManage)]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceDto>> Update(
        long id, [FromBody] UpdateDeviceRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new UpdateDeviceCommand(
            id, request.Name, request.IpAddress, request.Port, request.Model), cancellationToken));

    /// <summary>POST /devices/{id}/enable — resume polling. (FR-ZK-010.)</summary>
    [HttpPost("{id:long}/enable")]
    [HasPermission(Permissions.DeviceManage)]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DeviceDto>> Enable(long id, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SetDeviceEnabledCommand(id, true), cancellationToken));

    /// <summary>POST /devices/{id}/disable — stop polling (device retained). (FR-ZK-010.)</summary>
    [HttpPost("{id:long}/disable")]
    [HasPermission(Permissions.DeviceManage)]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DeviceDto>> Disable(long id, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SetDeviceEnabledCommand(id, false), cancellationToken));

    /// <summary>POST /devices/{id}/test-connection — probe reachability. (FR-ZK-010.)</summary>
    [HttpPost("{id:long}/test-connection")]
    [HasPermission(Permissions.DeviceManage)]
    [ProducesResponseType(typeof(TestDeviceConnectionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TestDeviceConnectionResult>> TestConnection(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new TestDeviceConnectionCommand(id), cancellationToken);
        return result.Reachable ? Ok(result) : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
    }

    /// <summary>POST /devices/{id}/sync-now — trigger an immediate sync via the handler. (FR-ZK, 05 §10.4.)</summary>
    [HttpPost("{id:long}/sync-now")]
    [HasPermission(Permissions.DeviceManage)]
    [ProducesResponseType(typeof(SyncDeviceResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<SyncDeviceResult>> SyncNow(long id, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SyncDeviceCommand(id), cancellationToken));

    /// <summary>GET /devices/{id}/sync-state — watermark & failure count. (FR-ZK-011 visibility.)</summary>
    [HttpGet("{id:long}/sync-state")]
    [HasPermission(Permissions.DeviceRead)]
    [ProducesResponseType(typeof(DeviceSyncStateDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DeviceSyncStateDto>> SyncState(long id, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetDeviceSyncStateQuery(id), cancellationToken));

    /// <summary>POST /devices/{id}/reconcile — prove device vs stored completeness. (FR-ZK-007.)</summary>
    [HttpPost("{id:long}/reconcile")]
    [HasPermission(Permissions.DeviceManage)]
    [ProducesResponseType(typeof(ReconcileDeviceResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReconcileDeviceResult>> Reconcile(long id, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new ReconcileDeviceCommand(id), cancellationToken));

    /// <summary>POST /devices/{id}/enrollments — enroll an employee on this device. (BRULE-09.)</summary>
    [HttpPost("{id:long}/enrollments")]
    [HasPermission(Permissions.DeviceManage)]
    [ProducesResponseType(typeof(EnrollmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EnrollmentDto>> Enroll(
        long id, [FromBody] EnrollRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new EnrollEmployeeCommand(request.EmployeeId, id, request.DeviceUserId), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>GET /devices/{id}/enrollments — list this device's enrollments.</summary>
    [HttpGet("{id:long}/enrollments")]
    [HasPermission(Permissions.DeviceRead)]
    [ProducesResponseType(typeof(IReadOnlyList<EnrollmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EnrollmentDto>>> GetEnrollments(long id, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetDeviceEnrollmentsQuery(id), cancellationToken));

    /// <summary>DELETE /devices/{id}/enrollments/{enrollmentId} — deactivate an enrollment (soft).</summary>
    [HttpDelete("{id:long}/enrollments/{enrollmentId:long}")]
    [HasPermission(Permissions.DeviceManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateEnrollment(long id, long enrollmentId, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeactivateEnrollmentCommand(enrollmentId), cancellationToken);
        return NoContent();
    }

    /// <summary>POST /devices/{id}/push-enrollments — push enrolled users to the terminal. (FR-ZK-003 outbound.)</summary>
    [HttpPost("{id:long}/push-enrollments")]
    [HasPermission(Permissions.DeviceManage)]
    [ProducesResponseType(typeof(SyncEnrollmentsToDeviceResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<SyncEnrollmentsToDeviceResult>> PushEnrollments(long id, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SyncEnrollmentsToDeviceCommand(id), cancellationToken));
}
