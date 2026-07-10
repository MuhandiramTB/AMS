using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;

namespace TAMS.Application.Devices;

public sealed record SyncEnrollmentsToDeviceResult(long DeviceId, int PushedCount);

/// <summary>
/// Pushes the device's active enrollments to the terminal (outbound half of
/// FR-ZK-003), so the hardware recognises the enrolled users. The real SDK adapter
/// provisions them; the simulator records the intent. Idempotent — safe to re-run.
/// </summary>
public sealed record SyncEnrollmentsToDeviceCommand(long DeviceId) : IRequest<SyncEnrollmentsToDeviceResult>;

public sealed class SyncEnrollmentsToDeviceHandler
    : IRequestHandler<SyncEnrollmentsToDeviceCommand, SyncEnrollmentsToDeviceResult>
{
    private readonly IDeviceRepository _devices;
    private readonly IDeviceGateway _gateway;

    public SyncEnrollmentsToDeviceHandler(IDeviceRepository devices, IDeviceGateway gateway)
    {
        _devices = devices;
        _gateway = gateway;
    }

    public async Task<SyncEnrollmentsToDeviceResult> Handle(
        SyncEnrollmentsToDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = await _devices.GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new NotFoundException("Device", request.DeviceId);

        var enrollments = await _devices.GetEnrollmentsForDeviceAsync(device.Id, cancellationToken);
        var activeUserIds = enrollments.Where(e => e.IsActive).Select(e => e.DeviceUserId).ToList();

        var connection = new DeviceConnection(device.Id, device.SerialNo, device.IpAddress, device.Port);
        var pushed = await _gateway.SyncEnrollmentsToDeviceAsync(connection, activeUserIds, cancellationToken);

        return new SyncEnrollmentsToDeviceResult(device.Id, pushed);
    }
}
