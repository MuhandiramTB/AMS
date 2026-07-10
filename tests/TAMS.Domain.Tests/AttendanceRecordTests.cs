using FluentAssertions;
using TAMS.Domain.Attendance;
using TAMS.Domain.Common;

namespace TAMS.Domain.Tests;

public sealed class AttendanceRecordTests
{
    private static readonly DateOnly WorkDate = new(2026, 7, 10);
    private static readonly DateTime Now = new(2026, 7, 10, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RecomputeWorkedFromInOut_HonoursBreakMinutes()
    {
        var record = new AttendanceRecord(employeeId: 1, WorkDate);
        record.SetFirstIn(new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc));
        record.SetLastOut(new DateTime(2026, 7, 10, 17, 0, 0, DateTimeKind.Utc));

        record.RecomputeWorkedFromInOut(breakMinutes: 60);

        record.WorkedMinutes.Should().Be(8 * 60 - 60); // 420
        record.Status.Should().Be(AttendanceStatus.Corrected);
    }

    [Fact]
    public void ApplyCorrection_RequiresReason()
    {
        var record = new AttendanceRecord(1, WorkDate);
        var act = () => record.ApplyCorrection(9, "FirstInUtc", null, "x", reason: "", Now);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ApplyCorrection_RecordsTrailAndMovesToCorrected()
    {
        var record = new AttendanceRecord(1, WorkDate);
        record.ApplyCorrection(9, "FirstInUtc", "09:25", "09:00", "CCTV confirms", Now);

        record.Corrections.Should().ContainSingle();
        record.Status.Should().Be(AttendanceStatus.Corrected);
    }

    [Fact]
    public void FinalizedRecord_CannotBeCorrectedOrRecalculated()
    {
        var record = new AttendanceRecord(1, WorkDate);
        record.FinalizeRecord();

        var correct = () => record.ApplyCorrection(9, "FirstInUtc", null, "x", "reason", Now);
        correct.Should().Throw<DomainException>();

        var recalc = () => record.ApplyCalculation(new AttendanceResult(), null);
        recalc.Should().Throw<DomainException>();
    }
}
