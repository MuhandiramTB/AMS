using System.Net.Http.Json;
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

    [Fact]
    public async Task NetworkDropMidDownload_PreservesWatermark_RecoversOnNextCycle()
    {
        // §9.2: network drop mid-download → partial handled safely, re-sync completes the set.
        var (id, serial) = await RegisterDeviceAsync("middrop");
        _factory.Simulator.EmitPunch(serial, "7", new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc), 1);
        _factory.Simulator.EmitPunch(serial, "7", new DateTime(2026, 7, 10, 17, 0, 0, DateTimeKind.Utc), 2);

        _factory.Simulator.FailNextDownload(serial);          // arm a mid-download drop
        var dropped = await _factory.SendAsync(new SyncDeviceCommand(id));
        dropped.Reachable.Should().BeFalse();
        dropped.WatermarkAdvanced.Should().BeFalse();
        (await _factory.CountPunchesAsync(id)).Should().Be(0); // nothing ingested, nothing lost

        var recovered = await _factory.SendAsync(new SyncDeviceCommand(id));
        recovered.Reachable.Should().BeTrue();
        (await _factory.CountPunchesAsync(id)).Should().Be(2); // full set recovered, exactly once
    }

    [Fact]
    public async Task Reconciliation_DetectsGap_WhenAPunchWasNotIngested()
    {
        // §9.2: reconciliation must SURFACE a gap, not only confirm the clean path.
        var (id, serial) = await RegisterDeviceAsync("recongap");
        _factory.Simulator.EmitPunch(serial, "3", new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc), 1);
        await _factory.SendAsync(new SyncDeviceCommand(id)); // ingests the first punch

        // A new punch appears on the device but is NOT synced yet.
        _factory.Simulator.EmitPunch(serial, "3", new DateTime(2026, 7, 10, 17, 0, 0, DateTimeKind.Utc), 2);

        var recon = await _factory.SendAsync(new ReconcileDeviceCommand(id));
        recon.Clean.Should().BeFalse();
        recon.MissingCount.Should().Be(1);
    }

    [Fact]
    public async Task EnrollingLater_BackfillsOrphanedPunches_IntoAttendance()
    {
        // HIGH fix: a punch captured before enrollment must not be orphaned — once
        // the employee is enrolled, the punch is resolved and processed. (FR-ZK-003.)
        var (id, serial) = await RegisterDeviceAsync("orphan");

        // Set up an employee + department to enroll to.
        var client = await AdminHttpClientAsync();
        await client.PostAsJsonAsync("/api/v1/departments", new { code = $"DORPH{id}", name = "Orphan Dept" });
        var deptId = await LastDeptIdAsync(client);
        var empResp = await client.PostAsJsonAsync("/api/v1/employees",
            new { employeeNo = $"ORPH{id}", firstName = "Ola", lastName = "Ph", primaryDepartmentId = deptId });
        var empId = (await empResp.Content.ReadFromJsonAsync<IdResp>())!.id;

        // Punch arrives BEFORE enrollment → stored unresolved.
        _factory.Simulator.EmitPunch(serial, "orphan-user", new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc), 1);
        _factory.Simulator.EmitPunch(serial, "orphan-user", new DateTime(2026, 7, 10, 17, 0, 0, DateTimeKind.Utc), 2);
        var sync = await _factory.SendAsync(new SyncDeviceCommand(id));
        sync.Unresolved.Should().Be(2);

        // Before enrollment: the punches are stored but attributed to no one.
        (await _factory.CountResolvedPunchesAsync(empId)).Should().Be(0);

        // Now enroll → back-fill + reprocess.
        await _factory.SendAsync(new EnrollEmployeeCommand(empId, id, "orphan-user"));

        // The previously-orphaned punches are now attributed to the employee and a
        // processed attendance record exists — no captured punch was lost.
        (await _factory.CountResolvedPunchesAsync(empId)).Should().Be(2);
        (await _factory.HasAttendanceRecordAsync(empId)).Should().BeTrue();
    }

    // --- helpers for the enrollment/backfill test (need the HTTP + query surface) ---
    private async Task<System.Net.Http.HttpClient> AdminHttpClientAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "admin", password = "ChangeMe!123" });
        var body = await login.Content.ReadFromJsonAsync<LoginResp>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!.accessToken);
        return client;
    }

    private static async Task<long> LastDeptIdAsync(System.Net.Http.HttpClient client)
    {
        var depts = await client.GetFromJsonAsync<List<IdResp>>("/api/v1/departments");
        return depts!.Last().id;
    }

    private sealed record LoginResp(string accessToken);
    private sealed record IdResp(long id);
}
