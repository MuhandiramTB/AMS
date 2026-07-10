using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;

namespace TAMS.Integration.Tests;

/// <summary>
/// HTTP-level tests for the device endpoints — exercising the real authorization
/// pipeline ([HasPermission]), model binding and error mapping that the MediatR-
/// direct resilience tests bypass. Covers §9.3 authz scenarios for the device
/// surface and the dup→409 contracts.
/// </summary>
[Collection("integration")]
public sealed class DeviceApiTests
{
    private readonly TamsWebApplicationFactory _factory;

    public DeviceApiTests(TamsWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> ClientAsAdminAsync()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "admin", password = "ChangeMe!123" });
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.accessToken);
        return client;
    }

    [Fact]
    public async Task RegisterDevice_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/devices",
            new { serialNo = "NA", name = "NA", ipAddress = (string?)null, port = (int?)null, model = (string?)null });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegisterDevice_AsAdmin_Succeeds_ThenDuplicateSerial_Returns409()
    {
        var client = await ClientAsAdminAsync();
        var serial = $"DUP-{Guid.NewGuid():N}".Substring(0, 16);

        var first = await client.PostAsJsonAsync("/api/v1/devices",
            new { serialNo = serial, name = "Gate", ipAddress = "10.0.0.1", port = 4370, model = "K40" });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = await client.PostAsJsonAsync("/api/v1/devices",
            new { serialNo = serial, name = "Gate2", ipAddress = "10.0.0.2", port = 4370, model = "K40" });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict); // 409 (G1 mapping / handler)
    }

    [Fact]
    public async Task DuplicateEnrollment_SameDeviceSlot_Returns409_BRULE09()
    {
        var client = await ClientAsAdminAsync();
        var serial = $"ENR-{Guid.NewGuid():N}".Substring(0, 16);
        var dev = await (await client.PostAsJsonAsync("/api/v1/devices",
            new { serialNo = serial, name = "EnrDev", ipAddress = "10.0.0.9", port = 4370, model = "K40" }))
            .Content.ReadFromJsonAsync<IdResponse>();

        await client.PostAsJsonAsync("/api/v1/departments", new { code = $"ED{dev!.id}", name = "Dept" });
        var depts = await client.GetFromJsonAsync<List<IdResponse>>("/api/v1/departments");
        var deptId = depts!.Last().id;
        var e1 = await (await client.PostAsJsonAsync("/api/v1/employees",
            new { employeeNo = $"EE{dev.id}", firstName = "A", lastName = "B", primaryDepartmentId = deptId }))
            .Content.ReadFromJsonAsync<IdResponse>();

        var first = await client.PostAsJsonAsync($"/api/v1/devices/{dev.id}/enrollments",
            new { employeeId = e1!.id, deviceUserId = "slot-1" });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Same (device, deviceUserId) slot again → 409 (BRULE-09).
        var dup = await client.PostAsJsonAsync($"/api/v1/devices/{dev.id}/enrollments",
            new { employeeId = e1.id, deviceUserId = "slot-1" });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private sealed record LoginResponse(string accessToken);
    private sealed record IdResponse(long id);
}
