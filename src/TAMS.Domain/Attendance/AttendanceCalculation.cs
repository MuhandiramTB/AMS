namespace TAMS.Domain.Attendance;

/// <summary>A single punch reduced to the inputs the calculator needs.</summary>
public sealed record PunchInput(DateTime PunchedAtUtc, PunchDirection Direction);

/// <summary>
/// The computed result of processing a day's punches against a shift.
/// Immutable value object — the pure output of <see cref="AttendanceCalculator"/>.
/// </summary>
public sealed record AttendanceResult
{
    public DateTime? FirstInUtc { get; init; }
    public DateTime? LastOutUtc { get; init; }
    public int? WorkedMinutes { get; init; }
    public int LateMinutes { get; init; }
    public int EarlyLeaveMinutes { get; init; }
    public int OvertimeMinutes { get; init; }
    public AttendanceStatus Status { get; init; }
    public IReadOnlyList<AttendanceExceptionType> Exceptions { get; init; } = [];
}
