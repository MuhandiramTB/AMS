using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;

namespace TAMS.Integration.Tests;

/// <summary>
/// Automated authN/authZ tests through the full HTTP pipeline — the Doc 10 §9.3
/// security scenarios and the Doc 10 §15 Phase-1 exit gate.
/// </summary>
[Collection("integration")]
public sealed class AuthorizationTests
{
    private readonly TamsWebApplicationFactory _factory;

    public AuthorizationTests(TamsWebApplicationFactory factory) => _factory = factory;

    private static async Task<string> LoginAsync(HttpClient client, string user = "admin", string pw = "ChangeMe!123")
    {
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = user, password = pw });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.accessToken;
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/employees");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401_Generic()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "admin", password = "wrong" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TamperedToken_IsRejected()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client) + "tampered";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.GetAsync("/api/v1/employees");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminHasPermission_CanListEmployees()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client));
        var resp = await client.GetAsync("/api/v1/employees?page=1&pageSize=10");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SecurityHeaders_ArePresentOnResponses()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/health/live");
        resp.Headers.Contains("X-Content-Type-Options").Should().BeTrue();
        resp.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        resp.Headers.Contains("X-Frame-Options").Should().BeTrue();
        resp.Headers.Contains("Content-Security-Policy").Should().BeTrue();
    }

    [Fact]
    public async Task OverPosting_ExtraFields_AreIgnored_NotBound()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client));
        await client.PostAsJsonAsync("/api/v1/departments", new { code = "OP1", name = "Ops" });

        // Attempt to over-post 'isActive=false' and an 'id' — must not corrupt state.
        var resp = await client.PostAsJsonAsync("/api/v1/employees",
            new { employeeNo = "OP-1", firstName = "Op", lastName = "One", primaryDepartmentId = 1,
                  id = 9999, isActive = false, status = "Terminated" });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await resp.Content.ReadFromJsonAsync<EmployeeResponse>();
        created!.isActive.Should().BeTrue();      // server default, not the injected false
        created.status.Should().Be("Active");     // not the injected 'Terminated'
        created.id.Should().NotBe(9999);           // surrogate key server-assigned
    }

    private sealed record LoginResponse(string accessToken);
    private sealed record EmployeeResponse(long id, bool isActive, string status);
}
