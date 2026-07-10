using TAMS.Domain.Common;

namespace TAMS.Domain.Devices;

public enum DeviceEventType : byte
{
    Sync = 1,
    Download = 2,
    Error = 3,
    Reconcile = 4,
    Alert = 5
}

public enum DeviceEventOutcome : byte
{
    Success = 1,
    Failure = 2,
    Retry = 3
}

/// <summary>
/// Insert-only diagnostic record of a device operation, tied to a correlation id
/// so a sync cycle can be traced end-to-end in the logs. (FR-ZK-009, 04 §6.4.)
/// </summary>
public sealed class DeviceEventLog : Entity
{
    private DeviceEventLog()
    {
    }

    public DeviceEventLog(
        long deviceId,
        DeviceEventType eventType,
        DeviceEventOutcome outcome,
        Guid correlationId,
        string? message,
        DateTime occurredAtUtc)
    {
        DeviceId = deviceId;
        EventType = eventType;
        Outcome = outcome;
        CorrelationId = correlationId;
        Message = message;
        OccurredAtUtc = occurredAtUtc;
    }

    public long DeviceId { get; private set; }
    public DeviceEventType EventType { get; private set; }
    public DeviceEventOutcome Outcome { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string? Message { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
}
