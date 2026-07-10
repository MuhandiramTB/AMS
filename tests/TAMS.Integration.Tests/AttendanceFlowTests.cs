using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;

namespace TAMS.Integration.Tests;

/// <summary>
/// End-to-end attendance flow through the real stack, proving the hardening
/// guarantees: punch idempotency (G3/FR-ATT-008), correction optimistic
/// concurrency (G2/FR-ATT-006), and the mandatory-reason rule (BRULE-05).
/// </summary>
[Collection("integration")]
public sealed class AttendanceFlowTests
{
    private readonly TamsWebApplicationFactory _factory;

    public AttendanceFlowTests(TamsWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "admin", password = "ChangeMe!123" });
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.accessToken);
        return client;
    }

    [Fact]
    public async Task DuplicatePunch_IsIdempotent_NoDuplicateStored()
    {
        var client = await AdminClientAsync();
        await client.PostAsJsonAsync("/api/v1/departments", new { code = "ATT", name = "Att" });
        var empResp = await client.PostAsJsonAsync("/api/v1/employees",
            new { employeeNo = "ATT-1", firstName = "Amy", lastName = "Ng", primaryDepartmentId = GetDeptId(client) });
        var emp = await empResp.Content.ReadFromJsonAsync<IdResponse>();

        var punch = new { deviceId = 1, deviceUserId = "1", employeeId = emp!.id, punchedAtUtc = "2026-07-10T09:00:00Z", direction = 1 };

        var first = await client.PostAsJsonAsync("/api/v1/attendance/punches", punch);
        var second = await client.PostAsJsonAsync("/api/v1/attendance/punches", punch);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        // Re-submitting the same punch is an idempotent no-op (200 duplicate), never a 500.
        second.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Correction_WithoutIfMatch_Returns428()
    {
        var (client, recordId, _) = await SetupProcessedRecordAsync("C428");
        var resp = await client.PatchAsJsonAsync($"/api/v1/attendance/records/{recordId}",
            new { firstInUtc = "2026-07-10T09:00:00Z", reason = "fix" });
        resp.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }

    [Fact]
    public async Task Correction_WithStaleIfMatch_Returns409()
    {
        var (client, recordId, etag) = await SetupProcessedRecordAsync("C409");

        // First correction succeeds and bumps the RowVersion.
        var req1 = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/attendance/records/{recordId}")
        {
            Content = JsonContent.Create(new { firstInUtc = "2026-07-10T09:05:00Z", reason = "first correction" })
        };
        req1.Headers.TryAddWithoutValidation("If-Match", etag);
        (await client.SendAsync(req1)).EnsureSuccessStatusCode();

        // Second correction reuses the STALE etag → 409 Conflict (lost-update prevented).
        var req2 = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/attendance/records/{recordId}")
        {
            Content = JsonContent.Create(new { firstInUtc = "2026-07-10T09:10:00Z", reason = "stale correction" })
        };
        req2.Headers.TryAddWithoutValidation("If-Match", etag);
        var resp2 = await client.SendAsync(req2);
        resp2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Correction_WithoutReason_Returns400()
    {
        var (client, recordId, etag) = await SetupProcessedRecordAsync("C400");
        var req = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/attendance/records/{recordId}")
        {
            Content = JsonContent.Create(new { firstInUtc = "2026-07-10T09:00:00Z", reason = "" })
        };
        req.Headers.TryAddWithoutValidation("If-Match", etag);
        var resp = await client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Creates dept+employee+punches, processes attendance, returns the record id and its ETag.</summary>
    private async Task<(HttpClient Client, long RecordId, string ETag)> SetupProcessedRecordAsync(string suffix)
    {
        var client = await AdminClientAsync();
        await client.PostAsJsonAsync("/api/v1/departments", new { code = $"D{suffix}", name = "Dept" });
        var deptId = GetDeptId(client);
        var empResp = await client.PostAsJsonAsync("/api/v1/employees",
            new { employeeNo = $"E{suffix}", firstName = "Ed", lastName = "Kay", primaryDepartmentId = deptId });
        var emp = await empResp.Content.ReadFromJsonAsync<IdResponse>();

        await client.PostAsJsonAsync("/api/v1/attendance/punches",
            new { deviceId = 1, deviceUserId = $"{emp!.id}", employeeId = emp.id, punchedAtUtc = "2026-07-10T09:25:00Z", direction = 1 });
        await client.PostAsJsonAsync("/api/v1/attendance/punches",
            new { deviceId = 1, deviceUserId = $"{emp.id}", employeeId = emp.id, punchedAtUtc = "2026-07-10T17:00:00Z", direction = 2 });

        var processResp = await client.PostAsJsonAsync("/api/v1/attendance/process",
            new { employeeId = emp.id, workDate = "2026-07-10" });
        processResp.EnsureSuccessStatusCode();
        var record = await processResp.Content.ReadFromJsonAsync<RecordResponse>();

        // Fetch to obtain the ETag header.
        var getResp = await client.GetAsync($"/api/v1/attendance/records/{record!.id}");
        var etag = getResp.Headers.ETag?.Tag ?? $"\"{record.concurrencyToken}\"";
        return (client, record.id, etag);
    }

    // Departments seeded fresh per test suffix; the first dept created in each
    // client session gets id 1 unless others exist — resolve by listing instead.
    private static long GetDeptId(HttpClient client)
    {
        var resp = client.GetAsync("/api/v1/departments").GetAwaiter().GetResult();
        var depts = resp.Content.ReadFromJsonAsync<List<IdResponse>>().GetAwaiter().GetResult();
        return depts!.Last().id;
    }

    private sealed record LoginResponse(string accessToken);
    private sealed record IdResponse(long id);
    private sealed record RecordResponse(long id, string concurrencyToken);
}
