using FluentAssertions;
using TAMS.Domain.Common;
using TAMS.Domain.Devices;

namespace TAMS.Domain.Tests;

public sealed class DeviceTests
{
    [Fact]
    public void Create_Valid_IsEnabled()
    {
        var device = new Device("ZK-1", "Gate A", "10.0.0.5", 4370, "K40");
        device.SerialNo.Should().Be("ZK-1");
        device.IsEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankSerial_Throws(string serial)
    {
        var act = () => new Device(serial, "Gate");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void DisableEnable_TogglesState()
    {
        var device = new Device("ZK-1", "Gate");
        device.Disable();
        device.IsEnabled.Should().BeFalse();
        device.Enable();
        device.IsEnabled.Should().BeTrue();
    }
}

public sealed class DeviceSyncStateTests
{
    private static readonly DateTime T = new(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CompleteSync_AdvancesWatermark_Forward()
    {
        var state = new DeviceSyncState(1);
        state.CompleteSync(T, T);
        state.LastWatermarkUtc.Should().Be(T);
    }

    [Fact]
    public void CompleteSync_NeverMovesWatermarkBackward()
    {
        var state = new DeviceSyncState(1);
        state.CompleteSync(T, T.AddHours(2));
        state.CompleteSync(T, T); // earlier watermark must be ignored
        state.LastWatermarkUtc.Should().Be(T.AddHours(2));
    }

    [Fact]
    public void CompleteSync_ResetsFailureCount()
    {
        var state = new DeviceSyncState(1);
        state.RecordFailure();
        state.RecordFailure();
        state.CompleteSync(T, T);
        state.ConsecutiveFailureCount.Should().Be(0);
    }

    [Fact]
    public void IsUnreachable_TrueOnlyAtThreshold()
    {
        var state = new DeviceSyncState(1);
        state.RecordFailure();
        state.RecordFailure();
        state.IsUnreachable(3).Should().BeFalse();
        state.RecordFailure();
        state.IsUnreachable(3).Should().BeTrue();
    }
}

public sealed class EnrollmentTests
{
    [Fact]
    public void Create_BlankDeviceUserId_Throws()
    {
        var act = () => new EmployeeDeviceEnrollment(1, 1, "");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Deactivate_SetsInactive()
    {
        var e = new EmployeeDeviceEnrollment(1, 1, "100");
        e.Deactivate();
        e.IsActive.Should().BeFalse();
    }
}

public sealed class PunchResolutionTests
{
    private static readonly DateTime T = new(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ResolveEmployee_SetsEmployeeId_WhenUnresolved()
    {
        var punch = new TAMS.Domain.Attendance.PunchTransaction(
            1, "u1", null, T, TAMS.Domain.Attendance.PunchDirection.In,
            TAMS.Domain.Attendance.PunchSource.Device, "key1", T);

        punch.ResolveEmployee(42);
        punch.EmployeeId.Should().Be(42);
    }

    [Fact]
    public void ResolveEmployee_Throws_WhenAlreadyResolved()
    {
        var punch = new TAMS.Domain.Attendance.PunchTransaction(
            1, "u1", 7, T, TAMS.Domain.Attendance.PunchDirection.In,
            TAMS.Domain.Attendance.PunchSource.Device, "key1", T);

        var act = () => punch.ResolveEmployee(42);
        act.Should().Throw<DomainException>();
    }
}
