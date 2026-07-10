using FluentAssertions;
using TAMS.Application.Devices;

namespace TAMS.Integration.Tests;

/// <summary>
/// The P3 exit gate (10 §9.2, KPI-04): prove — by actively injecting failure —
/// that ZKTeco capture loses no punches and never duplicates. Drives the real
/// SyncDeviceHandler (watermark + idempotent ingest + reconcile) against the
/// device simulator through real EF/SQL. If any of these fail, P3 is not done.
/// </summary>
[Collection("integration")]
public sealed class DeviceResilienceTests
{
    private readonly TamsWebApplicationFactory _factory;

    public DeviceResilienceTests(TamsWebApplicationFactory factory) => _factory = factory;

    /// <summary>Registers a uniquely-serialled device and returns its id + serial.</summary>
    private async Task<(long Id, string Serial)> RegisterDeviceAsync(string tag)
    {
        var serial = $"SIM-{tag}-{Guid.NewGuid():N}".Substring(0, 20);
        var dto = await _factory.SendAsync(new RegisterDeviceCommand(serial, $"Sim {tag}", "127.0.0.1", 4370, "SimModel"));
        _factory.Simulator.ClearBuffer(serial);
        return (dto.Id, serial);
    }

    [Fact]
    public async Task NormalSync_IngestsAllPunches_AdvancesWatermark()
    {
        var (id, serial) = await RegisterDeviceAsync("normal");
        _factory.Simulator.EmitPunch(serial, "100", new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc), 1);
        _factory.Simulator.EmitPunch(serial, "100", new DateTime(2026, 7, 10, 17, 0, 0, DateTimeKind.Utc), 2);

        var result = await _factory.SendAsync(new SyncDeviceCommand(id));

        result.Reachable.Should().BeTrue();
        result.Ingested.Should().Be(2);
        result.WatermarkAdvanced.Should().BeTrue();
        (await _factory.CountPunchesAsync(id)).Should().Be(2);
    }

    [Fact]
    public async Task ReSync_SameData_IsIdempotent_NoDuplicates()
    {
        var (id, serial) = await RegisterDeviceAsync("idem");
        _factory.Simulator.EmitPunch(serial, "1", new DateTime(2026, 7, 10, 8, 0, 0, DateTimeKind.Utc), 1);

        await _factory.SendAsync(new SyncDeviceCommand(id));
        var second = await _factory.SendAsync(new SyncDeviceCommand(id)); // re-run, same buffer

        second.Duplicates.Should().BeGreaterThanOrEqualTo(0);
        // The key guarantee: exactly one stored punch despite two syncs.
        (await _factory.CountPunchesAsync(id)).Should().Be(1);
    }

    [Fact]
    public async Task OutageThenRecovery_NoPermanentLoss_WatermarkPreservedDuringOutage()
    {
        var (id, serial) = await RegisterDeviceAsync("outage");
        _factory.Simulator.EmitPunch(serial, "5", new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc), 1);

        // 1) Device goes down BEFORE we sync the first punch.
        _factory.Simulator.SetOutage(serial, true);
        var during = await _factory.SendAsync(new SyncDeviceCommand(id));
        during.Reachable.Should().BeFalse();
        during.WatermarkAdvanced.Should().BeFalse();
        (await _factory.CountPunchesAsync(id)).Should().Be(0); // nothing yet, but nothing lost

        // 2) More punches accumulate on the device WHILE it is unreachable.
        _factory.Simulator.EmitPunch(serial, "5", new DateTime(2026, 7, 10, 17, 0, 0, DateTimeKind.Utc), 2);

        // 3) Device recovers → offline recovery must ingest BOTH the pre- and
        //    during-outage punches exactly once.
        _factory.Simulator.SetOutage(serial, false);
        var recovered = await _factory.SendAsync(new SyncDeviceCommand(id));
        recovered.Reachable.Should().BeTrue();
        (await _factory.CountPunchesAsync(id)).Should().Be(2); // zero permanent loss
    }

    [Fact]
    public async Task RepeatedOutage_RaisesUnreachableAlert()
    {
        var (id, serial) = await RegisterDeviceAsync("alert");
        _factory.Simulator.SetOutage(serial, true);

        SyncDeviceResult? last = null;
        for (var i = 0; i < 3; i++)
        {
            last = await _factory.SendAsync(new SyncDeviceCommand(id, UnreachableAlertThreshold: 3));
        }

        last!.Reachable.Should().BeFalse();
        last.Alerted.Should().BeTrue(); // FR-ZK-011: prolonged outage surfaces an alert
    }

    [Fact]
    public async Task Reconciliation_IsClean_WhenAllPunchesIngested()
    {
        var (id, serial) = await RegisterDeviceAsync("recon");
        _factory.Simulator.EmitPunch(serial, "9", new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc), 1);
        _factory.Simulator.EmitPunch(serial, "9", new DateTime(2026, 7, 10, 17, 0, 0, DateTimeKind.Utc), 2);
        await _factory.SendAsync(new SyncDeviceCommand(id));

        var recon = await _factory.SendAsync(new ReconcileDeviceCommand(id));

        recon.Clean.Should().BeTrue();
        recon.MissingCount.Should().Be(0);
        recon.DeviceCount.Should().Be(recon.StoredCount);
    }

    [Fact]
    public async Task UnenrolledPunch_IsCaptured_AsUnresolved_NotDropped()
    {
        var (id, serial) = await RegisterDeviceAsync("unres");
        // deviceUserId "999" is not enrolled to any employee.
        _factory.Simulator.EmitPunch(serial, "999", new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc), 1);

        var result = await _factory.SendAsync(new SyncDeviceCommand(id));

        result.Unresolved.Should().Be(1);
        // Crucially, the punch is STORED (not dropped) so an admin can fix enrollment.
        (await _factory.CountPunchesAsync(id)).Should().Be(1);
    }
}
