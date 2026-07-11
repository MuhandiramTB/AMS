using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TAMS.Infrastructure.Persistence;

namespace TAMS.Integration.Tests;

/// <summary>
/// The dev-only demo users (one per non-admin role) are seeded with the correct
/// role and are idempotent. Verifies each role login exists after seeding.
/// </summary>
[Collection("integration")]
public sealed class DemoUserSeedTests
{
    private readonly TamsWebApplicationFactory _factory;
    public DemoUserSeedTests(TamsWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("hr", "HROfficer")]
    [InlineData("manager", "Manager")]
    [InlineData("employee", "Employee")]
    [InlineData("auditor", "Auditor")]
    public async Task DemoUser_IsSeeded_WithExpectedRole(string userName, string expectedRole)
    {
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        // Idempotent — safe to run even though the collection DB already has the admin.
        await seeder.SeedAsync("admin", "admin@tams.local", "ChangeMe!123", seedDemoUsers: true);

        var db = scope.ServiceProvider.GetRequiredService<TamsDbContext>();
        var user = await db.Users.AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserName == userName);

        user.Should().NotBeNull($"demo user '{userName}' should be seeded in dev");
        user!.Roles.Should().ContainSingle(r => r.Name == expectedRole);
    }

    [Fact]
    public async Task Reseeding_DoesNotDuplicate_DemoUsers()
    {
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync("admin", "admin@tams.local", "ChangeMe!123", seedDemoUsers: true);
        await seeder.SeedAsync("admin", "admin@tams.local", "ChangeMe!123", seedDemoUsers: true);

        var db = scope.ServiceProvider.GetRequiredService<TamsDbContext>();
        var hrCount = await db.Users.CountAsync(u => u.UserName == "hr");
        hrCount.Should().Be(1);
    }
}
