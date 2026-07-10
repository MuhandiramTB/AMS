using FluentAssertions;
using TAMS.Domain.Common;
using TAMS.Domain.Leave;

namespace TAMS.Domain.Tests;

public sealed class LeaveRequestTests
{
    private static readonly DateTime Now = new(2026, 7, 10, 8, 0, 0, DateTimeKind.Utc);
    private static LeaveRequest New(DateOnly from, DateOnly to) => new(1, 2, from, to, "vacation");

    [Fact]
    public void DayCount_IsInclusive()
    {
        New(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14)).DayCount.Should().Be(3);
        New(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 12)).DayCount.Should().Be(1);
    }

    [Fact]
    public void Create_EndBeforeStart_Throws()
    {
        var act = () => New(new DateOnly(2026, 7, 14), new DateOnly(2026, 7, 12));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Approve_MovesToApproved_AndRecordsApprover()
    {
        var r = New(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14));
        r.Approve(approverUserId: 99, Now);
        r.Status.Should().Be(LeaveStatus.Approved);
        r.ApproverUserId.Should().Be(99);
    }

    [Fact]
    public void Approve_WhenNotSubmitted_Throws()
    {
        var r = New(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14));
        r.Approve(1, Now);
        var act = () => r.Approve(1, Now);   // already approved
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void CoversDate_TrueOnlyForApprovedOrApplied_WithinRange()
    {
        var r = New(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14));
        r.CoversDate(new DateOnly(2026, 7, 13)).Should().BeFalse(); // still Submitted
        r.Approve(1, Now);
        r.CoversDate(new DateOnly(2026, 7, 13)).Should().BeTrue();
        r.CoversDate(new DateOnly(2026, 7, 15)).Should().BeFalse(); // out of range
    }

    [Fact]
    public void MarkApplied_RequiresApproved()
    {
        var r = New(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 12));
        var act = () => r.MarkApplied();     // still Submitted
        act.Should().Throw<DomainException>();
        r.Approve(1, Now);
        r.MarkApplied();
        r.Status.Should().Be(LeaveStatus.Applied);
    }

    [Fact]
    public void Cancel_AllowedFromSubmittedOrApproved_NotFromRejected()
    {
        var r = New(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 12));
        r.Reject(1, Now);
        var act = () => r.Cancel();
        act.Should().Throw<DomainException>();
    }
}

public sealed class LeaveBalanceTests
{
    private static LeaveBalance New(decimal entitled) => new(1, 2, 2026, entitled);

    [Fact]
    public void Consume_WithinBalance_DecrementsRemaining()
    {
        var b = New(20m);
        b.Consume(5m);
        b.UsedDays.Should().Be(5m);
        b.RemainingDays.Should().Be(15m);
    }

    [Fact]
    public void Consume_OverBalance_Throws_UnlessOverride()
    {
        var b = New(3m);
        var act = () => b.Consume(5m);          // BRULE-07
        act.Should().Throw<DomainException>();

        b.Consume(5m, allowOverride: true);     // explicit override permitted
        b.RemainingDays.Should().Be(-2m);
    }

    [Fact]
    public void CanConsume_ReflectsRemaining()
    {
        var b = New(4m);
        b.CanConsume(4m).Should().BeTrue();
        b.CanConsume(5m).Should().BeFalse();
    }

    [Fact]
    public void Release_ReturnsDays_ButNeverBelowZero()
    {
        var b = New(10m);
        b.Consume(3m);
        b.Release(3m);
        b.UsedDays.Should().Be(0m);
        b.Release(5m); // extra release clamps at 0
        b.UsedDays.Should().Be(0m);
    }
}
