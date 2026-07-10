using TAMS.Domain.Common;

namespace TAMS.Domain.Attendance;

/// <summary>
/// An immutable raw biometric event — the source of truth. Never updated or
/// deleted (insert-only); corrections adjust the derived AttendanceRecord, not
/// the fact. Idempotency key guarantees exactly-once storage. (ADR-010/011,
/// FR-ATT-001/008, 04 §6.5.)
/// </summary>
public sealed class PunchTransaction : Entity
{
    private PunchTransaction()
    {
    }

    public PunchTransaction(
        long deviceId,
        string deviceUserId,
        long? employeeId,
        DateTime punchedAtUtc,
        PunchDirection direction,
        PunchSource sourceType,
        string idempotencyKey,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new DomainException("Idempotency key is required.");
        }

        DeviceId = deviceId;
        DeviceUserId = deviceUserId;
        EmployeeId = employeeId;
        PunchedAtUtc = punchedAtUtc;
        Direction = direction;
        SourceType = sourceType;
        IdempotencyKey = idempotencyKey;
        CreatedAtUtc = createdAtUtc;
    }

    public long DeviceId { get; private set; }
    public string DeviceUserId { get; private set; } = string.Empty;
    public long? EmployeeId { get; private set; }
    public DateTime PunchedAtUtc { get; private set; }
    public PunchDirection Direction { get; private set; }
    public PunchSource SourceType { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
}
