using MediatR;
using Microsoft.Extensions.Logging;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Attendance;
using TAMS.Domain.Common;
using TAMS.Domain.Devices;

namespace TAMS.Application.Devices;

/// <summary>Outcome of one device sync cycle.</summary>
public sealed record SyncDeviceResult(
    long DeviceId,
    bool Reachable,
    int Downloaded,
    int Ingested,
    int Duplicates,
    int Unresolved,
    bool WatermarkAdvanced,
    bool Alerted);

/// <summary>
/// Syncs a single device: incremental watermark-gated download → idempotent,
/// de-duplicating ingest → advance the watermark only after a durable store →
/// reconcile. A failure at any step leaves the watermark untouched so nothing is
/// skipped, and re-runs never duplicate — this is what makes capture loss-free and
/// exactly-once (ADR-011, KPI-04, FR-ZK-002/005/006/007/008/011).
/// </summary>
public sealed record SyncDeviceCommand(long DeviceId, int UnreachableAlertThreshold = 3)
    : IRequest<SyncDeviceResult>;

public sealed class SyncDeviceHandler : IRequestHandler<SyncDeviceCommand, SyncDeviceResult>
{
    private readonly IDeviceRepository _devices;
    private readonly IDeviceGateway _gateway;
    private readonly IAttendanceRepository _attendance;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICorrelationIdAccessor _correlation;
    private readonly ILogger<SyncDeviceHandler> _logger;

    public SyncDeviceHandler(
        IDeviceRepository devices,
        IDeviceGateway gateway,
        IAttendanceRepository attendance,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICorrelationIdAccessor correlation,
        ILogger<SyncDeviceHandler> logger)
    {
        _devices = devices;
        _gateway = gateway;
        _attendance = attendance;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _correlation = correlation;
        _logger = logger;
    }

    public async Task<SyncDeviceResult> Handle(SyncDeviceCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var correlationId = _correlation.CorrelationId;

        var device = await _devices.GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new Common.Exceptions.NotFoundException("Device", request.DeviceId);

        var state = await _devices.GetSyncStateAsync(device.Id, cancellationToken);
        if (state is null)
        {
            state = new DeviceSyncState(device.Id);
            await _devices.AddSyncStateAsync(state, cancellationToken);
        }

        state.BeginSync(now);
        var connection = new DeviceConnection(device.Id, device.SerialNo, device.IpAddress, device.Port);

        IReadOnlyList<DeviceTransaction> transactions;
        try
        {
            // Incremental: only transactions strictly after the watermark. (FR-ZK-002.)
            transactions = await _gateway.DownloadTransactionsAsync(connection, state.LastWatermarkUtc, cancellationToken);
        }
        catch (Exception ex)
        {
            // Outage: record the failure, DO NOT advance the watermark, maybe alert.
            state.RecordFailure();
            var alerted = state.IsUnreachable(request.UnreachableAlertThreshold);
            await LogAsync(device.Id, DeviceEventType.Error, DeviceEventOutcome.Failure, correlationId,
                $"Download failed: {ex.Message}", now, cancellationToken);
            if (alerted)
            {
                await LogAsync(device.Id, DeviceEventType.Alert, DeviceEventOutcome.Failure, correlationId,
                    $"Device unreachable for {state.ConsecutiveFailureCount} consecutive attempts.", now, cancellationToken);
                _logger.LogWarning(
                    "Device {DeviceId} unreachable x{Count} (CorrelationId {CorrelationId})",
                    device.Id, state.ConsecutiveFailureCount, correlationId);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new SyncDeviceResult(device.Id, Reachable: false, 0, 0, 0, 0, WatermarkAdvanced: false, alerted);
        }

        device.MarkSeen(now);

        // Idempotent ingest: each transaction becomes a punch keyed by a deterministic
        // idempotency key; duplicates (from retries/replays) are no-ops. (FR-ZK-008.)
        var ingested = 0;
        var duplicates = 0;
        var unresolved = 0;
        DateTime? maxPunchUtc = state.LastWatermarkUtc;

        foreach (var tx in transactions.OrderBy(t => t.PunchedAtUtc))
        {
            var employeeId = await _devices.ResolveEmployeeIdAsync(device.Id, tx.DeviceUserId, cancellationToken);
            if (employeeId is null)
            {
                // Never guess an owner: store unresolved for admin fix, don't drop. (BRULE-09.)
                unresolved++;
            }

            var direction = (PunchDirection)tx.Direction;
            var key = PunchIdempotency.BuildKey(device.Id, tx.DeviceUserId, tx.PunchedAtUtc, direction);
            var punch = new PunchTransaction(
                device.Id, tx.DeviceUserId, employeeId, tx.PunchedAtUtc,
                direction, PunchSource.Device, key, now);

            var added = await _attendance.TryAddPunchAsync(punch, cancellationToken);
            if (added)
            {
                ingested++;
            }
            else
            {
                duplicates++;
            }

            if (maxPunchUtc is null || tx.PunchedAtUtc > maxPunchUtc)
            {
                maxPunchUtc = tx.PunchedAtUtc;
            }
        }

        // Advance the watermark ONLY now that punches are durably stored. (ADR-011.)
        var advanced = maxPunchUtc != state.LastWatermarkUtc;
        state.CompleteSync(now, maxPunchUtc);

        await LogAsync(device.Id, DeviceEventType.Download, DeviceEventOutcome.Success, correlationId,
            $"Downloaded {transactions.Count}, ingested {ingested}, duplicates {duplicates}, unresolved {unresolved}.",
            now, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SyncDeviceResult(
            device.Id, Reachable: true, transactions.Count, ingested, duplicates, unresolved, advanced, Alerted: false);
    }

    private async Task LogAsync(
        long deviceId, DeviceEventType type, DeviceEventOutcome outcome,
        Guid correlationId, string message, DateTime now, CancellationToken cancellationToken)
        => await _devices.AddEventLogAsync(
            new DeviceEventLog(deviceId, type, outcome, correlationId, message, now), cancellationToken);
}
