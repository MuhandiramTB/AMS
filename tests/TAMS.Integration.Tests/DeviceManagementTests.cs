using FluentAssertions;
using TAMS.Application.Devices;

namespace TAMS.Integration.Tests;

/// <summary>
/// Tests for the P3 MEDIUM/LOW hardening: device edit/enable/disable, enrollment
/// list/deactivate, outbound enrollment sync (FR-ZK-003), and the same-timestamp
/// watermark boundary. MediatR-direct for stability (matches resilience tests).
/// </summary>
[Collection("integration")]
public sealed class DeviceManagementTests
{
    private readonly TamsWebApplicationFactory _factory;

    public DeviceManagementTests(TamsWebApplicationFactory factory) => _factory = factory;

    private async Task<(long Id, string Serial)> RegisterAsync(string tag)
    {
        var serial = $"MG-{tag}-{Guid.NewGuid():N}".Substring(0, 18);
        var dto = await _factory.SendAsync(new RegisterDeviceCommand(serial, $"Mgmt {tag}", "127.0.0.1", 4370, "K40"));
        _factory.Simulator.ClearBuffer(serial);
        return (dto.Id, serial);
    }

    [Fact]
    public async Task EnableDisable_TogglesPollingEligibility()
    {
        var (id, _) = await RegisterAsync("toggle");
        var disabled = await _factory.SendAsync(new SetDeviceEnabledCommand(id, false));
        disabled.IsEnabled.Should().BeFalse();
        var enabled = await _factory.SendAsync(new SetDeviceEnabledCommand(id, true));
        enabled.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Update_ChangesDetails()
    {
        var (id, _) = await RegisterAsync("edit");
        var updated = await _factory.SendAsync(new UpdateDeviceCommand(id, "Renamed Gate", "10.1.1.1", 5000, "K50"));
        updated.Name.Should().Be("Renamed Gate");
        updated.IpAddress.Should().Be("10.1.1.1");
        updated.Port.Should().Be(5000);
    }

    [Fact]
    public async Task Enrollment_ListAndDeactivate()
    {
        var (id, _) = await RegisterAsync("enr");
        var dept = await _factory.SendAsync(new TAMS.Application.Departments.CreateDepartmentCommand($"MDE{id}", "D", null));
        var emp = await _factory.SendAsync(new TAMS.Application.Employees.CreateEmployeeCommand($"MEE{id}", "M", "E", null, dept.Id, null));

        var enrollment = await _factory.SendAsync(new EnrollEmployeeCommand(emp.Id, id, "u-1"));
        var list = await _factory.SendAsync(new GetDeviceEnrollmentsQuery(id));
        list.Should().ContainSingle(e => e.Id == enrollment.Id && e.IsActive);

        await _factory.SendAsync(new DeactivateEnrollmentCommand(enrollment.Id));
        var afterList = await _factory.SendAsync(new GetDeviceEnrollmentsQuery(id));
        afterList.Should().ContainSingle(e => e.Id == enrollment.Id && !e.IsActive);
    }

    [Fact]
    public async Task PushEnrollments_ProvisionsActiveUsersToDevice()
    {
        var (id, serial) = await RegisterAsync("push");
        var dept = await _factory.SendAsync(new TAMS.Application.Departments.CreateDepartmentCommand($"MPD{id}", "D", null));
        var emp = await _factory.SendAsync(new TAMS.Application.Employees.CreateEmployeeCommand($"MPE{id}", "P", "U", null, dept.Id, null));
        await _factory.SendAsync(new EnrollEmployeeCommand(emp.Id, id, "push-user"));

        var result = await _factory.SendAsync(new SyncEnrollmentsToDeviceCommand(id));

        result.PushedCount.Should().Be(1);
        _factory.Simulator.KnownUsers(serial).Should().Contain("push-user");
    }

    [Fact]
    public async Task SameTimestampLateArrival_IsNotSkipped_ByInclusiveWatermark()
    {
        // LOW fix: a punch arriving at exactly the current watermark instant (but
        // with a distinct key) must still be ingested on a later sync.
        var (id, serial) = await RegisterAsync("boundary");
        var t = new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc);

        _factory.Simulator.EmitPunch(serial, "1", t, 1);           // In at T
        await _factory.SendAsync(new SyncDeviceCommand(id));        // watermark = T
        (await _factory.CountPunchesAsync(id)).Should().Be(1);

        // A different punch at the SAME instant T (distinct direction → distinct key).
        _factory.Simulator.EmitPunch(serial, "1", t, 2);           // Out at T
        var second = await _factory.SendAsync(new SyncDeviceCommand(id));

        // Inclusive (>=) re-fetch + idempotent de-dup → the boundary punch is captured,
        // the already-stored one is not duplicated.
        (await _factory.CountPunchesAsync(id)).Should().Be(2);
    }
}
