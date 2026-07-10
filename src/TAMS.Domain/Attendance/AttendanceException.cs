using TAMS.Domain.Common;

namespace TAMS.Domain.Attendance;

/// <summary>An anomaly on a record requiring review. (FR-ATT-005, 04 §6.5.)</summary>
public sealed class AttendanceException : Entity
{
    private AttendanceException()
    {
    }

    public AttendanceException(long attendanceRecordId, AttendanceExceptionType exceptionType)
    {
        AttendanceRecordId = attendanceRecordId;
        ExceptionType = exceptionType;
        IsResolved = false;
    }

    public long AttendanceRecordId { get; private set; }
    public AttendanceExceptionType ExceptionType { get; private set; }
    public bool IsResolved { get; private set; }
    public long? ResolvedByUserId { get; private set; }
    public string? Notes { get; private set; }

    public void Resolve(long userId, string? notes)
    {
        IsResolved = true;
        ResolvedByUserId = userId;
        Notes = notes;
    }
}
