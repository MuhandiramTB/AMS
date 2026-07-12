using FluentAssertions;
using TAMS.Domain.Attendance;
using TAMS.Domain.Scheduling;

namespace TAMS.Domain.Tests;

/// <summary>
/// Exhaustive, table-driven tests for the accuracy-critical AttendanceCalculator
/// (10 §9.1, ADR-006, G-01). Because the calculator is pure, every rule and edge
/// is covered here at the fast unit level. All times UTC.
/// </summary>
public sealed class AttendanceCalculatorTests
{
    private static readonly DateOnly WorkDate = new(2026, 7, 10);
    private readonly AttendanceCalculator _calc = new();

    private static Shift DayShift(int graceIn = 10, int graceOut = 10, int breakMin = 60, int otThreshold = 0) =>
        new("DAY", "Day", new TimeOnly(9, 0), new TimeOnly(17, 0), breakMin, graceIn, graceOut, otThreshold);

    private static Shift NightShift() =>
        new("NIGHT", "Night", new TimeOnly(22, 0), new TimeOnly(6, 0), breakMinutes: 60, graceInMinutes: 10, graceOutMinutes: 10);

    private static PunchInput In(int h, int m) => new(new DateTime(2026, 7, 10, h, m, 0, DateTimeKind.Utc), PunchDirection.In);
    private static PunchInput Out(int h, int m) => new(new DateTime(2026, 7, 10, h, m, 0, DateTimeKind.Utc), PunchDirection.Out);

    [Fact]
    public void NormalDay_WithinShift_ComputesWorkedMinutesMinusBreak_NoAnomalies()
    {
        var result = _calc.Calculate(WorkDate, DayShift(), [In(9, 0), Out(17, 0)]);

        result.Status.Should().Be(AttendanceStatus.Processed);
        result.WorkedMinutes.Should().Be(8 * 60 - 60); // 8h gross − 1h break = 420
        result.LateMinutes.Should().Be(0);
        result.EarlyLeaveMinutes.Should().Be(0);
        result.OvertimeMinutes.Should().Be(0);
        result.Exceptions.Should().BeEmpty();
    }

    [Fact]
    public void ArrivalWithinGrace_IsNotLate()
    {
        var result = _calc.Calculate(WorkDate, DayShift(graceIn: 10), [In(9, 8), Out(17, 0)]);
        result.LateMinutes.Should().Be(0);
    }

    [Fact]
    public void ArrivalBeyondGrace_IsLateByExcessOnly()
    {
        // 25 min late, 10 min grace → 15 late minutes.
        var result = _calc.Calculate(WorkDate, DayShift(graceIn: 10), [In(9, 25), Out(17, 0)]);
        result.LateMinutes.Should().Be(15);
    }

    [Fact]
    public void EarlyDeparture_BeyondGrace_IsEarlyLeave()
    {
        // Left 16:40 = 20 min early, 10 grace → 10 early-leave minutes.
        var result = _calc.Calculate(WorkDate, DayShift(graceOut: 10), [In(9, 0), Out(16, 40)]);
        result.EarlyLeaveMinutes.Should().Be(10);
    }

    [Fact]
    public void WorkBeyondEnd_PastThreshold_AccruesOvertime()
    {
        // Out 18:00 = 60 min beyond 17:00; threshold 0 → 60 OT.
        var result = _calc.Calculate(WorkDate, DayShift(otThreshold: 0), [In(9, 0), Out(18, 0)]);
        result.OvertimeMinutes.Should().Be(60);
    }

    [Fact]
    public void WorkBeyondEnd_WithinThreshold_NoOvertime()
    {
        // 20 min beyond end but threshold is 30 → no OT.
        var result = _calc.Calculate(WorkDate, DayShift(otThreshold: 30), [In(9, 0), Out(17, 20)]);
        result.OvertimeMinutes.Should().Be(0);
    }

    [Fact]
    public void WorkBeyondEnd_PastThreshold_CreditsOnlyExcessOverThreshold()
    {
        // 60 min beyond end, threshold 30 → only the 30 minutes past the threshold count.
        var result = _calc.Calculate(WorkDate, DayShift(otThreshold: 30), [In(9, 0), Out(18, 0)]);
        result.OvertimeMinutes.Should().Be(30);
    }

    [Fact]
    public void OvernightShift_CrossingMidnight_ComputesWorkedHoursCorrectly()
    {
        // Night shift 22:00 → 06:00 next day. In 22:00, out 06:00 (+1 day).
        var punchIn = new PunchInput(new DateTime(2026, 7, 10, 22, 0, 0, DateTimeKind.Utc), PunchDirection.In);
        var punchOut = new PunchInput(new DateTime(2026, 7, 11, 6, 0, 0, DateTimeKind.Utc), PunchDirection.Out);

        var result = _calc.Calculate(WorkDate, NightShift(), [punchIn, punchOut]);

        result.Status.Should().Be(AttendanceStatus.Processed);
        result.WorkedMinutes.Should().Be(8 * 60 - 60); // 8h − 1h break
        result.LateMinutes.Should().Be(0);
        result.EarlyLeaveMinutes.Should().Be(0);
    }

    [Fact]
    public void MissingOut_RaisesMissingOutException_NoWorkedTotal()
    {
        var result = _calc.Calculate(WorkDate, DayShift(), [In(9, 0)]);

        result.Status.Should().Be(AttendanceStatus.Exception);
        result.Exceptions.Should().Contain(AttendanceExceptionType.MissingOut);
        result.WorkedMinutes.Should().BeNull();
        result.FirstInUtc.Should().NotBeNull();
    }

    [Fact]
    public void NoPunches_NoLeave_RaisesMissingIn()
    {
        var result = _calc.Calculate(WorkDate, DayShift(), []);

        result.Status.Should().Be(AttendanceStatus.Exception);
        result.Exceptions.Should().Contain(AttendanceExceptionType.MissingIn);
    }

    [Fact]
    public void NoPunches_LeaveCovered_IsProcessedNotAbsent()
    {
        // BRULE-06 / FR-ATT-007: approved leave overrides absence.
        var result = _calc.Calculate(WorkDate, DayShift(), [], isLeaveCovered: true);

        result.Status.Should().Be(AttendanceStatus.Processed);
        result.Exceptions.Should().BeEmpty();
    }

    [Fact]
    public void UnknownDirections_FallBackToEarliestAndLatest()
    {
        var p1 = new PunchInput(new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc), PunchDirection.Unknown);
        var p2 = new PunchInput(new DateTime(2026, 7, 10, 17, 0, 0, DateTimeKind.Utc), PunchDirection.Unknown);

        var result = _calc.Calculate(WorkDate, DayShift(), [p2, p1]); // out of order on purpose

        result.FirstInUtc!.Value.Hour.Should().Be(9);
        result.LastOutUtc!.Value.Hour.Should().Be(17);
        result.WorkedMinutes.Should().Be(420);
    }

    [Fact]
    public void PunchesOutOfOrder_AreSortedBeforeComputing()
    {
        var result = _calc.Calculate(WorkDate, DayShift(), [Out(17, 0), In(9, 0)]);
        result.WorkedMinutes.Should().Be(420);
    }

    [Fact]
    public void NoShiftAssigned_StillComputesWorkedMinutes_NoLateOrOt()
    {
        var result = _calc.Calculate(WorkDate, shift: null, [In(9, 0), Out(17, 0)]);

        result.Status.Should().Be(AttendanceStatus.Processed);
        result.WorkedMinutes.Should().Be(8 * 60); // no break without a shift
        result.LateMinutes.Should().Be(0);
        result.OvertimeMinutes.Should().Be(0);
    }

    [Fact]
    public void LateAndOvertime_CanCoexist()
    {
        // In 09:30 (20 late after 10 grace), out 18:00 (60 OT).
        var result = _calc.Calculate(WorkDate, DayShift(graceIn: 10, otThreshold: 0), [In(9, 30), Out(18, 0)]);
        result.LateMinutes.Should().Be(20);
        result.OvertimeMinutes.Should().Be(60);
    }
}
