using MediatR;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Attendance;

namespace TAMS.Application.Devices;

/// <summary>Reconciliation outcome: proves completeness by comparing device vs stored.</summary>
public sealed record ReconcileDeviceResult(long DeviceId, int DeviceCount, int StoredCount, int MissingCount, bool Clean);

/// <summary>
/// Compares the full set of transactions the device holds against what we have
/// ingested, so we can *prove* no punches were lost rather than assume it. A
/// non-empty missing-set is the one condition that escalates to an incident.
/// (FR-ZK-007, KPI-04.)
/// </summary>
public sealed record ReconcileDeviceCommand(long DeviceId) : IRequest<ReconcileDeviceResult>;

public sealed class ReconcileDeviceHandler : IRequestHandler<ReconcileDeviceCommand, ReconcileDeviceResult>
{
    private readonly IDeviceRepository _devices;
    private readonly IDeviceGateway _gateway;
    private readonly IAttendanceRepository _attendance;

    public ReconcileDeviceHandler(IDeviceRepository devices, IDeviceGateway gateway, IAttendanceRepository attendance)
    {
        _devices = devices;
        _gateway = gateway;
        _attendance = attendance;
    }

    public async Task<ReconcileDeviceResult> Handle(ReconcileDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = await _devices.GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new Common.Exceptions.NotFoundException("Device", request.DeviceId);

        var connection = new DeviceConnection(device.Id, device.SerialNo, device.IpAddress, device.Port);
        var deviceTx = await _gateway.ListAllTransactionsAsync(connection, cancellationToken);

        // Build the set of idempotency keys the device claims to hold.
        var deviceKeys = deviceTx
            .Select(t => PunchIdempotency.BuildKey(device.Id, t.DeviceUserId, t.PunchedAtUtc, (PunchDirection)t.Direction))
            .ToHashSet();

        var storedKeys = await _attendance.GetPunchKeysForDeviceAsync(device.Id, cancellationToken);
        var storedSet = storedKeys.ToHashSet();

        var missing = deviceKeys.Count(k => !storedSet.Contains(k));

        return new ReconcileDeviceResult(
            device.Id, deviceKeys.Count, storedSet.Count, missing, Clean: missing == 0);
    }
}
