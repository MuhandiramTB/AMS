using FluentAssertions;
using TAMS.Domain.Common;
using TAMS.Domain.Scheduling;

namespace TAMS.Domain.Tests;

public sealed class ShiftTests
{
    [Fact]
    public void DayShift_IsNotOvernight_ScheduledMinutesExcludeBreak()
    {
        var shift = new Shift("DAY", "Day", new TimeOnly(9, 0), new TimeOnly(17, 0), breakMinutes: 60);
        shift.IsOvernight.Should().BeFalse();
        shift.ScheduledMinutes.Should().Be(7 * 60); // 8h − 1h break
    }

    [Fact]
    public void OvernightShift_IsDetected_AndResolvesEndToNextDay()
    {
        var shift = new Shift("NIGHT", "Night", new TimeOnly(22, 0), new TimeOnly(6, 0));
        shift.IsOvernight.Should().BeTrue();

        var (start, end) = shift.ResolveWindow(new DateOnly(2026, 7, 10));
        start.Should().Be(new DateTime(2026, 7, 10, 22, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 7, 11, 6, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void NegativeMinutes_Throws()
    {
        var act = () => new Shift("X", "X", new TimeOnly(9, 0), new TimeOnly(17, 0), graceInMinutes: -1);
        act.Should().Throw<DomainException>();
    }
}

public sealed class ShiftAssignmentTests
{
    private static readonly DateOnly From = new(2026, 7, 1);

    [Fact]
    public void ForEmployee_IsEffectiveWithinRange()
    {
        var a = ShiftAssignment.ForEmployee(shiftId: 1, employeeId: 5, From, new DateOnly(2026, 7, 31));
        a.IsEffectiveOn(new DateOnly(2026, 7, 15)).Should().BeTrue();
        a.IsEffectiveOn(new DateOnly(2026, 8, 1)).Should().BeFalse();
        a.IsEffectiveOn(new DateOnly(2026, 6, 30)).Should().BeFalse();
    }

    [Fact]
    public void OpenEnded_IsEffectiveIndefinitely()
    {
        var a = ShiftAssignment.ForDepartment(1, 2, From);
        a.IsEffectiveOn(new DateOnly(2030, 1, 1)).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_DetectsIntersectingRanges()
    {
        var a = ShiftAssignment.ForEmployee(1, 5, From, new DateOnly(2026, 7, 31));
        a.Overlaps(new DateOnly(2026, 7, 15), new DateOnly(2026, 8, 15)).Should().BeTrue();
        a.Overlaps(new DateOnly(2026, 8, 1), null).Should().BeFalse();
    }

    [Fact]
    public void EffectiveToBeforeFrom_Throws()
    {
        var act = () => ShiftAssignment.ForEmployee(1, 5, From, new DateOnly(2026, 6, 1));
        act.Should().Throw<DomainException>();
    }
}

public sealed class ShiftResolverTests
{
    private static readonly DateOnly Date = new(2026, 7, 10);
    private readonly ShiftResolver _resolver = new();

    [Fact]
    public void EmployeeAssignment_WinsOverDepartment()
    {
        var assignments = new[]
        {
            ShiftAssignment.ForDepartment(shiftId: 100, departmentId: 9, new DateOnly(2026, 1, 1)),
            ShiftAssignment.ForEmployee(shiftId: 200, employeeId: 5, new DateOnly(2026, 1, 1)),
        };

        var resolved = _resolver.ResolveShiftId(Date, employeePrimaryDepartmentId: 9, assignments);
        resolved.Should().Be(200);
    }

    [Fact]
    public void FallsBackToDepartment_WhenNoEmployeeAssignment()
    {
        var assignments = new[] { ShiftAssignment.ForDepartment(100, 9, new DateOnly(2026, 1, 1)) };
        _resolver.ResolveShiftId(Date, 9, assignments).Should().Be(100);
    }

    [Fact]
    public void ReturnsNull_WhenNothingEffective()
    {
        var assignments = new[] { ShiftAssignment.ForEmployee(100, 5, new DateOnly(2027, 1, 1)) };
        _resolver.ResolveShiftId(Date, 9, assignments).Should().BeNull();
    }
}
