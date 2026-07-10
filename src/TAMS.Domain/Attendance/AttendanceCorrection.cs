using TAMS.Domain.Common;

namespace TAMS.Domain.Attendance;

/// <summary>
/// An append-only record of a manual correction, preserving the original value.
/// A reason is mandatory (BRULE-05). Corrections adjust the derived record, never
/// the raw punch. (FR-ATT-006, 04 §6.5.)
/// </summary>
public sealed class AttendanceCorrection : Entity
{
    private AttendanceCorrection()
    {
    }

    public AttendanceCorrection(
        long attendanceRecordId,
        long correctedByUserId,
        string fieldName,
        string? oldValue,
        string? newValue,
        string reason,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A correction reason is required.");
        }

        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new DomainException("A correction must name the field being changed.");
        }

        AttendanceRecordId = attendanceRecordId;
        CorrectedByUserId = correctedByUserId;
        FieldName = fieldName;
        OldValue = oldValue;
        NewValue = newValue;
        Reason = reason.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public long AttendanceRecordId { get; private set; }
    public long CorrectedByUserId { get; private set; }
    public string FieldName { get; private set; } = string.Empty;
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
}
