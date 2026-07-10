using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TAMS.Infrastructure.Persistence;

namespace TAMS.Integration.Tests;

/// <summary>
/// Hosts the real API in-process against a dedicated LocalDB test database
/// (10 §3/§7 — integration tests use a real EF/test DB, not the device or prod DB).
/// The DB is created, migrated and seeded once per test-class collection and
/// dropped on dispose, so tests exercise the full HTTP → MediatR → EF → SQL stack.
/// </summary>
public sealed class TamsWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestConnection =
        "Server=(localdb)\\MSSQLLocalDB;Database=TAMS_IntegrationTests;Trusted_Connection=True;TrustServerCertificate=true;MultipleActiveResultSets=true";

    // xunit calls this once when the collection fixture is created.
    public async Task InitializeAsync() => await ResetDatabaseAsync();

    // Note: xunit disposes the fixture (via IDisposable) at collection end; the
    // async counterpart is a no-op so we don't drop the DB mid-run.
    async Task IAsyncLifetime.DisposeAsync() => await Task.CompletedTask;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = TestConnection,
                // Deterministic, self-contained test secrets (never real).
                ["Jwt:SigningKey"] = "integration-tests-only-signing-key-at-least-32-bytes-long!!",
                ["Seed:Enabled"] = "true",
                ["BootstrapAdmin:UserName"] = "admin",
                ["BootstrapAdmin:Email"] = "admin@tams.local",
                ["BootstrapAdmin:Password"] = "ChangeMe!123",
                // All tests share one host IP; relax rate limits so the limiter
                // (verified separately) doesn't throttle the suite. Prod keeps defaults.
                ["RateLimit:AuthPerMinute"] = "100000",
                ["RateLimit:GlobalPerMinute"] = "100000",
            });
        });
    }

    /// <summary>The in-memory device simulator (singleton) for driving device behaviour in tests.</summary>
    public TAMS.Infrastructure.Devices.SimulatedDeviceGateway Simulator =>
        (TAMS.Infrastructure.Devices.SimulatedDeviceGateway)
        Services.GetRequiredService<TAMS.Application.Common.Ports.IDeviceGateway>();

    /// <summary>Runs a MediatR request within a fresh DI scope (as the worker would).</summary>
    public async Task<T> SendAsync<T>(MediatR.IRequest<T> request)
    {
        using var scope = Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
        return await mediator.Send(request);
    }

    /// <summary>
    /// Counts DEVICE-SOURCED punches for a device (to assert zero loss / no dup).
    /// Filters to PunchSource.Device so manual-entry punches from other test classes
    /// sharing the collection can't contaminate the count.
    /// </summary>
    public async Task<int> CountPunchesAsync(long deviceId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TamsDbContext>();
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            db.Punches.Where(p =>
                p.DeviceId == deviceId &&
                p.SourceType == TAMS.Domain.Attendance.PunchSource.Device));
    }

    /// <summary>
    /// Counts punches on a specific device now attributed to an employee (verifies
    /// orphan back-fill). Device-scoped so parallel test data can't contaminate it.
    /// </summary>
    public async Task<int> CountResolvedPunchesAsync(long employeeId, long deviceId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TamsDbContext>();
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            db.Punches.Where(p => p.EmployeeId == employeeId && p.DeviceId == deviceId));
    }

    /// <summary>True if a processed attendance record exists for the employee/date.</summary>
    public async Task<bool> HasAttendanceRecordAsync(long employeeId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TamsDbContext>();
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
            db.AttendanceRecords.Where(r => r.EmployeeId == employeeId));
    }

    /// <summary>Ensures a clean, migrated, seeded database before the tests run.</summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TamsDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync("admin", "admin@tams.local", "ChangeMe!123");
    }

    private bool _dropped;

    protected override void Dispose(bool disposing)
    {
        // Drop the test DB once, before the host's service provider is torn down.
        if (disposing && !_dropped)
        {
            _dropped = true;
            try
            {
                using var scope = Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TamsDbContext>();
                db.Database.EnsureDeleted();
            }
            catch (ObjectDisposedException)
            {
                // Provider already torn down (double-dispose) — nothing to clean.
            }
        }

        base.Dispose(disposing);
    }
}
