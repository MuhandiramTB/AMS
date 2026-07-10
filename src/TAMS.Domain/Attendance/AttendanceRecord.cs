using TAMS.Domain.Common;

namespace TAMS.Domain.Attendance;

/// <summary>
/// The processed daily attendance result for one employee — derived from raw
/// punches, recomputable, and the target of corrections. One record per employee
/// per day. (FR-ATT-002/006/009, 02 §7.1, 04 §6.5.)
/// </summary>
public sealed class AttendanceRecord : AuditableEntity
{
    private readonly List<AttendanceException> _exceptions = new();
    private readonly List<AttendanceCorrection> _corrections = new();

    private AttendanceRecord()
    {
    }

    public AttendanceRecord(long employeeId, DateOnly workDate)
    {
        EmployeeId = employeeId;
        WorkDate = workDate;
        Status = AttendanceStatus.Pending;
    }

    public long EmployeeId { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public long? ResolvedShiftId { get; private set; }
    public DateTime? FirstInUtc { get; private set; }
    public DateTime? LastOutUtc { get; private set; }
    public int? WorkedMinutes { get; private set; }
    public int LateMinutes { get; private set; }
    public int EarlyLeaveMinutes { get; private set; }
    public int OvertimeMinutes { get; private set; }
    public AttendanceStatus Status { get; private set; }

    public IReadOnlyCollection<AttendanceException> Exceptions => _exceptions.AsReadOnly();
    public IReadOnlyCollection<AttendanceCorrection> Corrections => _corrections.AsReadOnly();

    /// <summary>
    /// Applies a (re)calculation result, replacing computed values and refreshing
    /// exceptions. Finalized records are immutable to recomputation. (FR-ATT-009.)
    /// </summary>
    public void ApplyCalculation(AttendanceResult result, long? resolvedShiftId)
    {
        if (Status == AttendanceStatus.Finalized)
        {
            throw new DomainException("A finalized attendance record cannot be recalculated.");
        }

        ResolvedShiftId = resolvedShiftId;
        FirstInUtc = result.FirstInUtc;
        LastOutUtc = result.LastOutUtc;
        WorkedMinutes = result.WorkedMinutes;
        LateMinutes = result.LateMinutes;
        EarlyLeaveMinutes = result.EarlyLeaveMinutes;
        OvertimeMinutes = result.OvertimeMinutes;

        // Refresh unresolved system exceptions to match the new result; keep any
        // already-resolved ones for history.
        _exceptions.RemoveAll(e => !e.IsResolved);
        foreach (var type in result.Exceptions)
        {
            _exceptions.Add(new AttendanceException(Id, type));
        }

        Status = result.Status;
    }

    /// <summary>
    /// Records a manual correction (with mandatory reason, preserving the original)
    /// and moves the record to Corrected. Recalculation is orchestrated by the
    /// application layer after the corrected inputs are applied. (FR-ATT-006, BRULE-05.)
    /// </summary>
    public void ApplyCorrection(
        long userId,
        string fieldName,
        string? oldValue,
        string? newValue,
        string reason,
        DateTime nowUtc)
    {
        if (Status == AttendanceStatus.Finalized)
        {
            throw new DomainException("A finalized attendance record cannot be corrected.");
        }

        _corrections.Add(new AttendanceCorrection(Id, userId, fieldName, oldValue, newValue, reason, nowUtc));
        Status = AttendanceStatus.Corrected;
    }

    /// <summary>Sets a corrected first-in (used only via a recorded correction).</summary>
    public void SetFirstIn(DateTime firstInUtc) => FirstInUtc = firstInUtc;

    /// <summary>Sets a corrected last-out (used only via a recorded correction).</summary>
    public void SetLastOut(DateTime lastOutUtc) => LastOutUtc = lastOutUtc;

    /// <summary>
    /// Re-derives worked minutes from the current (possibly corrected) in/out.
    /// If both are present and valid, the record moves to Corrected and any
    /// missing-punch exceptions are cleared. (FR-ATT-006/009.)
    /// </summary>
    public void RecomputeWorkedFromInOut(int breakMinutes = 0)
    {
        if (FirstInUtc is not null && LastOutUtc is not null && LastOutUtc > FirstInUtc)
        {
            var gross = (int)Math.Round((LastOutUtc.Value - FirstInUtc.Value).TotalMinutes);
            WorkedMinutes = Math.Max(0, gross - breakMinutes);
            _exceptions.RemoveAll(e =>
                !e.IsResolved &&
                (e.ExceptionType == AttendanceExceptionType.MissingIn ||
                 e.ExceptionType == AttendanceExceptionType.MissingOut));
            if (Status != AttendanceStatus.Finalized)
            {
                Status = AttendanceStatus.Corrected;
            }
        }
    }

    public void BeginReview()
    {
        if (Status == AttendanceStatus.Exception)
        {
            Status = AttendanceStatus.UnderReview;
        }
    }

    public void FinalizeRecord() => Status = AttendanceStatus.Finalized;
}
